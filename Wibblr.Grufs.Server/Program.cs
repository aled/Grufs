using System.Buffers;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

using Wibblr.Grufs.Storage;

namespace Wibblr.Grufs.Server
{
    public class Program
    {
        // address MUST be 64 hex characters, upper case only.
        private static SearchValues<char> validAddressChars = SearchValues.Create("0123456789ABCDEF");

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddAuthorization();

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseAuthorization();

            app.MapGet("{basedir}/chunk/{address}", async Task<Results<FileContentHttpResult, NotFound>> (HttpContext context, CancellationToken token, string baseDir, string address) =>
            {
                var chunkStorage = new LocalStorage(Path.Join("grufs-repo", baseDir));
                await chunkStorage.InitAsync(token);

                var chunk = await chunkStorage.GetAsync(new Address(Convert.FromHexString(address)), token);

                if (chunk is EncryptedChunk c)
                {
                    return TypedResults.File(c.Content, "application/octet-stream", address);
                }

                return TypedResults.NotFound();
            })
            .WithName("GetChunk")
            .WithOpenApi();

            // Takes an array of 48-bit address prefixes and returns a list of all existing addresses that match
            app.MapPost("/chunk/list-by-prefix", async Task<Results<Ok<byte[]>, BadRequest>> (HttpContext context, CancellationToken token) =>
            {
                using var stream = context.Request.Body;

                var contentLength = context.Request.GetTypedHeaders().ContentLength;

                if (contentLength == null)
                {
                    return TypedResults.BadRequest();
                }

                if (contentLength % 6 != 0)
                {
                    return TypedResults.BadRequest();
                }

                if (contentLength > 600)
                {
                    return TypedResults.BadRequest();
                }

                var buf = new byte[(int)contentLength];
                await stream.ReadExactlyAsync(buf);

                var numPrefixes = contentLength / 6;

                for (int i = 0; i < numPrefixes; i++)
                {
                    var prefix = Convert.ToHexString(buf.AsSpan(i, 6));

                    // TODO
                }

                return TypedResults.BadRequest();
            })
            .WithName("ListChunksByPrefix")
            .WithOpenApi();

            app.MapPut("{basedir}/chunk/{address}", async Task<Results<Ok, Conflict, BadRequest>> (HttpContext context, CancellationToken token, string baseDir, string address, [FromQuery] bool verifyChecksum = true) =>
            {
                if (address.Length != 64)
                {
                    return TypedResults.BadRequest();
                }

                if (address.AsSpan().IndexOfAnyExcept(validAddressChars) != -1)
                {
                    return TypedResults.BadRequest();
                }

                using var contentStream = context.Request.Body;

                var contentLength = context.Request.GetTypedHeaders().ContentLength switch
                {
                    long l when l <= 100 * 1024 * 1024 => (int) l,
                    _ => 0,
                };;

                if (contentLength == 0)
                {
                    return TypedResults.BadRequest();
                }
             

                var chunkStorage = new LocalStorage(Path.Join("grufs-repo", baseDir));
                await chunkStorage.InitAsync(token);

                var content = new byte[contentLength];
                await context.Request.Body.ReadExactlyAsync(content, token);

                var addressObj = new Address(Convert.FromHexString(address));

                var putStatus = await chunkStorage.PutAsync(new EncryptedChunk(addressObj, content), OverwriteStrategy.Deny, token);

                if (putStatus == PutStatus.Success)
                {
                    if (verifyChecksum)
                    {
                        if (!await VerifyChunkAsync(chunkStorage, addressObj, token))
                        {
                            throw new Exception("Checksum validation failure");
                        }
                    }

                    return TypedResults.Ok();
                }
                else if (putStatus == PutStatus.OverwriteDenied)
                {
                    return TypedResults.Conflict();
                }

                throw new Exception("Error");
            })
            .WithName("PutChunk")
            .WithOpenApi();

            app.Run();
        }

        private static async Task<bool> VerifyChunkAsync(IChunkStorage storage, Address address, CancellationToken token)
        {
            if (await storage.GetAsync(address, token) is not EncryptedChunk chunk)
            {
                return false;
            }

            var length = chunk.Content.Length;

            if (length < 32)
            {
                return false;
            }

            var computedHash = Convert.ToHexString(SHA256.HashData(chunk.Content.AsSpan(0, length - 32)));
            var actualHash = Convert.ToHexString(chunk.Content.AsSpan(length - 32));

            if (computedHash != actualHash)
            {
                return false;
            }

            return true;
        }
    }
}

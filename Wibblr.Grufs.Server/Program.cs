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

        private static LocalStorage _chunkStorage = new LocalStorage("grufs-repo");

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

            app.MapGet("/chunk/{address}", async Task<Results<Ok<byte[]>, NotFound>> (HttpContext context, CancellationToken token, string address) =>
            {
                var chunk = await _chunkStorage.GetAsync(new Address(Convert.FromHexString(address)), token);

                if (chunk is EncryptedChunk c)
                {
                    return TypedResults.Ok(c.Content);
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

            app.MapPut("/chunk/{address}", async Task<Results<Ok, Conflict, BadRequest>> (HttpContext context, CancellationToken token, string address, [FromQuery] bool verifyChecksum = true) =>
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

                if (context.Request.GetTypedHeaders().ContentLength > 100 * 1024 * 1024)
                {
                    return TypedResults.BadRequest();
                }

                if (File.Exists(address))
                {
                    return TypedResults.Conflict();
                }

                // TODO: use LocalStorage
                var partFileName = address + ".part";
                using var partFile = File.OpenWrite(partFileName);
                if (partFile.Length != 0)
                {
                    partFile.SetLength(0);
                }

                await context.Request.Body.CopyToAsync(partFile);
                partFile.Close();

                if (verifyChecksum)
                {
                    if (!await VerifyChunkAsync(partFileName))
                    {
                        throw new Exception("Checksum validation failure");
                    }
                }

                File.Move(partFileName, address, overwrite: false);

                return TypedResults.Ok();
            })
            .WithName("PutChunk")
            .WithOpenApi();

            app.Run();
        }

        private static async Task<bool> VerifyChunkAsync(string filename)
        {
            using var file = File.Open(filename, FileMode.Open, FileAccess.ReadWrite);  // not actually writing but need to block other writers
            var length = file.Length;

            var bytesToVerify = (int)(length - 32);

            if (bytesToVerify < 0)
            {
                return false;
            }

            var sha256 = SHA256.Create();
            var bufSize = 32;
            var buf = new byte[bufSize];

            var lastBufSize = bytesToVerify % bufSize;
            var numFullBufs = (bytesToVerify - lastBufSize) / bufSize;

            for (int i = 0; i < numFullBufs; i++)
            {
                await file.ReadExactlyAsync(buf);
                sha256.TransformBlock(buf, 0, bufSize, null, 0);
            }

            await file.ReadExactlyAsync(buf, 0, lastBufSize);
            sha256.TransformFinalBlock(buf, 0, lastBufSize);

            // Read checksum
            await file.ReadExactlyAsync(buf, 0, 32);

            var computedHash = sha256.Hash!;

            if (!computedHash.SequenceEqual(buf.Take(32)))
            {
                return false;
            }

            return true;
        }
    }
}

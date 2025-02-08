using System.Net;

namespace Wibblr.Grufs.Storage.Server
{
    public class HttpStorage : IChunkStorage, IDisposable
    {
        private static HttpClient _httpClient = new HttpClient();
        
        public string Host { get; init; }
        public int Port { get; init; }
        public string BaseDir { get; init; }

        public HttpStorage(string host, int port, string baseDir)
        {
            Host = host;
            Port = port;
            BaseDir = baseDir;
        }

        public Task InitAsync(CancellationToken token)
        {
            // TODO: write new Init method on http server
            return Task.CompletedTask;
        }

        public Task<long> CountAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public async Task<bool> ExistsAsync(Address address, CancellationToken token)
        {
            return await GetAsync(address, token) != null;
        }

        public async Task<EncryptedChunk?> GetAsync(Address address, CancellationToken token)
        {
            // Call GET /chunk/{address}
            try
            {
                var responseMessage = await _httpClient.GetAsync($"http://{Host}:{Port}/{BaseDir}/chunk/{address}", token);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    var content = await responseMessage.Content.ReadAsByteArrayAsync();

                    return new EncryptedChunk(address, content);
                }
            }
            catch
            {
            }

            return null;
        }

        public IAsyncEnumerable<Address> ListAddressesAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public async Task<PutStatus> PutAsync(EncryptedChunk chunk, OverwriteStrategy overwriteStrategy, CancellationToken token)
        {
            // Call PUT /chunk/{address}
            try
            {
                var httpResponse = await _httpClient.PutAsync($"http://{Host}:{Port}/{BaseDir}/chunk/{chunk.Address}", new ByteArrayContent(chunk.Content), token);

                if (httpResponse.StatusCode == HttpStatusCode.OK)
                {
                    return PutStatus.Success;
                }
                if (httpResponse.StatusCode == HttpStatusCode.Conflict)
                {
                    return PutStatus.OverwriteDenied;
                }
                return PutStatus.Error;
            }
            catch (Exception)
            {
                return PutStatus.Error;
            }
        }

        public void Flush()
        {
            // no op
        }

        public void Dispose()
        {
            _httpClient?.CancelPendingRequests();
        }
    }
}

using System.IO;
using System.Threading.Tasks;
using VSRAD.DebugServer;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;

namespace VSRAD.DebugServerTests.Handlers
{
    static class Helper
    {
        public static async Task<TR> DispatchCommandAsync<TC, TR>(TC command)
            where TC : ICommand where TR : IResponse
        {
            // Test serialization
            command = await command.WithSerializationAsync();

            var logger = new ClientLogger(0, false);
            var response = await Dispatcher.DispatchAsync(command, logger);

            using var stream = new MemoryStream();
            await stream.WriteSerializedMessageAsync(response);
            stream.Position = 0;
            // Test response serialization
            return (TR)await stream.ReadSerializedMessageAsync<IResponse>();
        }

        public static async Task<T> WithSerializationAsync<T>(this T command) where T : ICommand
        {
            using var stream = new MemoryStream();
            await stream.WriteSerializedMessageAsync(command);
            stream.Position = 0;
            return (T)await stream.ReadSerializedMessageAsync<ICommand>();
        }
    }
}

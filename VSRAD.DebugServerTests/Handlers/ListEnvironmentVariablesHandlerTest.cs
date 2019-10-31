using System;
using System.Collections;
using VSRAD.DebugServer.IPC.Commands;
using VSRAD.DebugServer.IPC.Responses;
using Xunit;

namespace VSRAD.DebugServerTests.Handlers
{
    public class ListEnvironmentVariablesHandlerTest
    {
        [Fact]
        public async void EnvironmentVariablesTest()
        {
            var envVars = Environment.GetEnvironmentVariables();

            // Verify that the command is dispatched correctly
            var response = await Helper.DispatchCommandAsync<ListEnvironmentVariables, EnvironmentVariablesListed>(
                new ListEnvironmentVariables());

            // Ensure that serialization/deserialization works as expected
            Assert.Equal(envVars.Count, response.Variables.Count);
            foreach (DictionaryEntry e in envVars)
                Assert.Equal((string)e.Value, response.Variables[(string)e.Key]);
        }
    }
}

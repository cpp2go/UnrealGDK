using System;
using System.Collections.Generic;
using System.Linq;
using Improbable.Worker;
using Improbable.Worker.Alpha;

namespace Improbable.WorkerCoordinator
{
    class Authentication
    {
        private const string LOCATOR_HOST_NAME = "locator.improbable.io";
        private const int LOCATOR_PORT = 444;

        public static string GetDevelopmentPlayerIdentityToken(string devAuthTokenId, string clientName)
        {
            var pitResponse = DevelopmentAuthentication.CreateDevelopmentPlayerIdentityTokenAsync("locator.improbable.io", 444,
                    new PlayerIdentityTokenRequest
                    {
                        DevelopmentAuthenticationTokenId = devAuthTokenId,
                        PlayerId = clientName,
                        DisplayName = clientName
                    }).Get();

            if (pitResponse.Status.Code != ConnectionStatusCode.Success)
            {
                throw new Exception($"Failed to retrieve player identity token.\n" +
                    $"error code: {pitResponse.Status.Code}\n" +
                    $"error message: {pitResponse.Status.Detail}");
            }

            return pitResponse.PlayerIdentityToken;
        }

        public static List<LoginTokenDetails> GetDevelopmentLoginTokens(string workerType, string pit)
        {
            var loginTokensResponse = DevelopmentAuthentication.CreateDevelopmentLoginTokensAsync(LOCATOR_HOST_NAME, LOCATOR_PORT,
                new LoginTokensRequest
                {
                    PlayerIdentityToken = pit,
                    WorkerType = workerType,
                    UseInsecureConnection = false,
                    DurationSeconds = 300
                }).Get();

            if (loginTokensResponse.Status.Code != ConnectionStatusCode.Success)
            {
                throw new Exception($"Failed to retrieve any login tokens.\n" +
                    $"error code: {loginTokensResponse.Status.Code}\n" +
                    $"error message: {loginTokensResponse.Status.Detail}");
            }

            return loginTokensResponse.LoginTokens;
        }

        public static string SelectLoginToken(List<LoginTokenDetails> loginTokens, string targetDeployment)
        {
            var selectedLoginToken = loginTokens.FirstOrDefault(token => token.DeploymentName == targetDeployment).LoginToken;

            if (selectedLoginToken == null)
            {
                throw new Exception("Failed to launch simulated player. Login token for target deployment was not found in response. Does that deployment have the `dev_auth` tag?");
            }

            return selectedLoginToken;
        }
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ConferenceHubFunctions.Authorization
{
    public class JwtValidator
    {
        private readonly IConfiguration _configuration;
        private readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager;

        public JwtValidator(IConfiguration configuration)
        {
            _configuration = configuration;
            var tenantId = _configuration["AzureAd:TenantId"];
            var metadataAddress = $"https://login.microsoftonline.com/{tenantId}/v2.0/.well-known/openid-configuration";
            _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                metadataAddress,
                new OpenIdConnectConfigurationRetriever());
        }

        public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
        {
            try
            {
                var config = await _configManager.GetConfigurationAsync();
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = config.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _configuration["AzureAd:ClientId"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = config.SigningKeys,
                    ValidateLifetime = true
                };

                var handler = new JwtSecurityTokenHandler();
                var principal = handler.ValidateToken(token, validationParameters, out _);
                return principal;
            }
            catch
            {
                return null;
            }
        }
    }
}

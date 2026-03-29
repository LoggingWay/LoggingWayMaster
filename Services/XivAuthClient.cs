using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoggingWayGrpcService.Services
{
    public record XivAuthUser(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("mfa_enabled")] bool MfaEnabled,
    [property: JsonPropertyName("verified_characters")] bool VerifiedCharacters,

    // user:email scope
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("email_verified")] bool? EmailVerified,

    // user:social scope
    [property: JsonPropertyName("social_identities")] List<XivAuthSocialIdentity>? SocialIdentities,

    [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt
);

    public record XivAuthSocialIdentity(
        [property: JsonPropertyName("provider")] string Provider,
        [property: JsonPropertyName("external_id")] string ExternalId,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("nickname")] string? Nickname,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
        [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt
    );

    public record XivAuthCharacter(
        [property: JsonPropertyName("persistent_key")] string PersistentKey,
        [property: JsonPropertyName("lodestone_id")] string LodestoneId,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("home_world")] string HomeWorld,
        [property: JsonPropertyName("data_center")] string DataCenter,
        [property: JsonPropertyName("avatar_url")] string AvatarUrl,
        [property: JsonPropertyName("portrait_url")] string PortraitUrl,
        [property: JsonPropertyName("created_at")] DateTimeOffset CreatedAt,
        [property: JsonPropertyName("verified_at")] DateTimeOffset? VerifiedAt,
        [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt
    );
    //This is needed for AOT compiling,reflection is not allowed at runtime
    [JsonSerializable(typeof(JsonElement))]
    [JsonSerializable(typeof(XivAuthUser))]
    [JsonSerializable(typeof(XivAuthCharacter))]
    [JsonSerializable(typeof(List<XivAuthCharacter>))]
    [JsonSerializable(typeof(XivAuthSocialIdentity))]
    [JsonSerializable(typeof(List<XivAuthSocialIdentity>))]
    internal partial class XivAuthJsonContext : JsonSerializerContext { }
    public class XivAuthClient(HttpClient httpClient, IConfiguration config)
    {
        private const string BaseUrl = "https://xivauth.net";

        private readonly string _clientId = config["XivAuth:ClientId"]!;
        private readonly string _clientSecret = config["XivAuth:ClientSecret"]!;
        private readonly string _redirectUri = config["XivAuth:RedirectUri"]!;

        public string BuildRedirectUri(string state, IEnumerable<string>? extraScopes = null)
        {
            var scopes = new HashSet<string> { "user", "character:all" };
            if (extraScopes != null)
                foreach (var s in extraScopes) scopes.Add(s);

            var query = QueryString.Create(new Dictionary<string, string?>
            {
                ["client_id"] = _clientId,
                ["redirect_uri"] = _redirectUri,
                ["response_type"] = "code",
                ["scope"] = string.Join(" ", scopes),
                ["state"] = state
            });

            return $"{BaseUrl}/oauth/authorize{query}";
        }

        public async Task<string> ExchangeCodeForAccessToken(string code)
        {
            var response = await httpClient.PostAsync($"{BaseUrl}/oauth/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "authorization_code",
                    ["client_id"] = _clientId,
                    ["client_secret"] = _clientSecret,
                    ["redirect_uri"] = _redirectUri,
                    ["code"] = code
                }));

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync(XivAuthJsonContext.Default.JsonElement);
            return json.GetProperty("access_token").GetString()!;
        }

        public async Task<XivAuthUser> GetUser(string accessToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/v1/user");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();

            return (await response.Content.ReadFromJsonAsync(XivAuthJsonContext.Default.XivAuthUser))!;
        }

        public async Task<List<XivAuthCharacter>?> GetCharacters(string accessToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/v1/characters");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await httpClient.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return null; // scope not granted — not an error

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync(XivAuthJsonContext.Default.ListXivAuthCharacter);
        }


        //Do everything in one blow
        public async Task<(XivAuthUser User, List<XivAuthCharacter>? Characters)> GetUserInfoFromCode(string code)
        {
            var accessToken = await ExchangeCodeForAccessToken(code);
            var user = await GetUser(accessToken);
            var characters = await GetCharacters(accessToken);//can be null if denied/no characters filled/no characters at time of login
            return (user, characters);
        }
    }
}

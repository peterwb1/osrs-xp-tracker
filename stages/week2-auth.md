# Weekend 2 — Auth and Ownership

## Goal

Lock the API down so that every tracked account belongs to a specific user, and users can't see or modify each other's data. By the end of this weekend you can register two users, log in as each, add different accounts, and confirm they're fully isolated. This is the foundation everything else builds on — get it right here and the later endpoints are straightforward.

## Starting Point

```
api/
├── OsrsTracker.Api/
│   ├── Controllers/AccountsController.cs   ← POST /api/accounts (no auth)
│   ├── Data/AppDbContext.cs                 ← inherits DbContext
│   ├── Data/SkillSeeder.cs
│   ├── Hiscores/HiscoresClient.cs
│   ├── Migrations/20260526_InitialCreate.*
│   └── Program.cs
├── OsrsTracker.Domain/
│   └── Models/ Skill, TrackedAccount, XpSnapshot
└── OsrsTracker.Tests/
    └── HiscoresParserTests.cs
```

`TrackedAccount` has no `UserId`. There's no users table, no JWT, no `[Authorize]`.

---

## Tasks

- [ ] Add NuGet packages to `OsrsTracker.Api`:
  - `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
  - `Microsoft.AspNetCore.Authentication.JwtBearer`
- [ ] Change `AppDbContext` to inherit from `IdentityDbContext<IdentityUser>` (adds 5 Identity tables automatically)
- [ ] Add `string UserId` property to `TrackedAccount`; add FK configuration in `AppDbContext.OnModelCreating`
- [ ] Create migration: `dotnet-ef migrations add AddAuthAndOwnership --project OsrsTracker.Api --startup-project OsrsTracker.Api`
- [ ] Register Identity + JWT in `Program.cs` (see Key Patterns below)
- [ ] Add `Jwt:Key` and `Jwt:Issuer` to `appsettings.json` (dev values only — use User Secrets or env vars for real values)
- [ ] Create `OsrsTracker.Api/Controllers/AuthController.cs` with:
  - `POST /api/auth/register` — email + password → creates user, returns JWT
  - `POST /api/auth/login` — credentials → validates, returns JWT
  - `GET /api/auth/me` — `[Authorize]` → returns current user's email + id
- [ ] Update `AccountsController`:
  - Add `[Authorize]` to the class
  - On `POST /api/accounts`: set `account.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier)`
- [ ] Add xUnit integration tests (add `Microsoft.AspNetCore.Mvc.Testing` package to test project):
  - Register a user → response contains a JWT
  - Login with correct credentials → returns JWT
  - Login with wrong password → returns 401
  - `POST /api/accounts` without token → returns 401
  - `POST /api/accounts` as user A, then try to access it as user B → returns 403/404

---

## Choices

### 1. JWT signing key storage
| Option | Pros | Cons |
|--------|------|------|
| `appsettings.json` | Simple, zero setup | Key in source control — never do this in prod |
| `dotnet user-secrets` | Key stays off disk in a separate store | Local only, needs setup per machine |
| Environment variable | Works everywhere, consistent with prod | Need to set it in your shell or docker-compose |

**Recommendation:** Use `appsettings.json` for the key name/issuer, but read the actual secret from an env var: `builder.Configuration["Jwt:Key"]` will fall through to `JWT__KEY` env var automatically. For local dev, add it to `appsettings.Development.json` (which is gitignored).

### 2. Token lifetime
| Option | Pros | Cons |
|--------|------|------|
| Short-lived (15 min) + refresh token | Secure, limits exposure window | Requires refresh token infrastructure |
| Long-lived (7 days) | Simple — no refresh logic | If token is stolen, valid for 7 days |

**Recommendation:** Long-lived for now. Add a comment noting it should be shortened when refresh tokens are added. This is a personal project and you're building the foundation — don't over-engineer auth before you need it.

### 3. UserId type in TrackedAccount
| Option | Pros | Cons |
|--------|------|------|
| `string` (matches IdentityUser.Id) | No type conversion needed | Less readable in queries |
| `int` (requires custom IdentityUser) | Simpler FK column | Requires creating a custom `ApplicationUser : IdentityUser` class |

**Recommendation:** Keep `string`. The standard `IdentityUser.Id` is a GUID string. Changing it requires inheriting `IdentityUser<TKey>` everywhere — not worth it for this project.

### 4. Password validation rules
The default Identity validator requires: 6+ chars, uppercase, lowercase, digit, special char.

**Recommendation:** Relax for dev speed:
```csharp
options.Password.RequireDigit = false;
options.Password.RequireUppercase = false;
options.Password.RequireNonAlphanumeric = false;
options.Password.RequiredLength = 8;
```
Leave a comment that production should tighten these.

---

## Research Topics

**What is ASP.NET Identity?**
A membership system built into ASP.NET Core. It manages: user storage (hashed passwords, email, etc.), password validation, and a `UserManager<TUser>` service for creating/finding/deleting users. It does NOT handle tokens — that's separate. You use it to create users and validate passwords, then you build the JWT yourself.

**How does JWT work?**
A JWT is three base64url-encoded strings joined by dots: `header.payload.signature`. The header says which algorithm (HS256). The payload contains *claims* — key-value pairs like `{ "sub": "user-guid", "email": "x@y.com", "exp": 1234567890 }`. The signature is HMAC-SHA256 of `header.payload` using your secret key. The server validates incoming tokens by re-computing the signature — if it matches, the token is authentic. No database lookup needed.

**What is `IdentityDbContext<IdentityUser>`?**
When you change `AppDbContext : DbContext` to `AppDbContext : IdentityDbContext<IdentityUser>`, EF Core knows to include ASP.NET Identity's tables in your migrations: `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetUserLogins`. Your `DbSet<Skill>` etc. still work the same way — you just get the Identity tables added automatically.

**What is a JWT claim?**
A claim is a statement about the user stored in the token payload. Standard claims: `sub` (subject = user ID), `email`, `exp` (expiry Unix timestamp), `jti` (unique token ID). In ASP.NET Core, you read them with `User.FindFirstValue(ClaimTypes.NameIdentifier)` (which maps to `sub`) inside a controller action. Claims are set when you *create* the token and can't be changed without issuing a new one.

**Why isn't `[Authorize]` enough for ownership?**
`[Authorize]` confirms the request has a valid JWT — the user is authenticated. But it doesn't confirm *which user* is making the request. Without an ownership check, user A could `DELETE /api/accounts/42` even if account 42 belongs to user B. You must always do: `if (account.UserId != currentUserId) return Forbid();`

**What is `WebApplicationFactory<T>`?**
A test utility in `Microsoft.AspNetCore.Mvc.Testing` that spins up your real ASP.NET Core app in-process during tests. You can make HTTP requests against it without running a real server. Use it to write integration tests that test the full stack (routing → controller → database) rather than just unit tests.

---

## Key Patterns

### Registering Identity + JWT in Program.cs
```csharp
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options => {
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 8;
})
.AddEntityFrameworkStores<AppDbContext>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

// Must come before app.MapControllers()
app.UseAuthentication();
app.UseAuthorization();
```

### Creating a JWT in AuthController
```csharp
var claims = new[] {
    new Claim(ClaimTypes.NameIdentifier, user.Id),
    new Claim(ClaimTypes.Email, user.Email!)
};
var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
var token = new JwtSecurityToken(
    issuer: _config["Jwt:Issuer"],
    claims: claims,
    expires: DateTime.UtcNow.AddDays(7),
    signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
return new JwtSecurityTokenHandler().WriteToken(token);
```

### Reading the current user in a controller
```csharp
var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
    ?? throw new InvalidOperationException("No user ID in token");
```

---

## Verify

```bash
# Register
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"alice@example.com","password":"password123"}'
# → { "token": "eyJ..." }

# Try adding account without token → 401
curl -X POST http://localhost:5000/api/accounts \
  -H "Content-Type: application/json" \
  -d '{"osrsUsername":"Zezima"}'

# Add account with token
curl -X POST http://localhost:5000/api/accounts \
  -H "Authorization: Bearer eyJ..." \
  -H "Content-Type: application/json" \
  -d '{"osrsUsername":"Zezima"}'

# Register second user, try to access first user's account → 403/404
```

Run tests: `dotnet test` — all should pass including new auth integration tests.

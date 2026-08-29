using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using InternLink.Web.Data;
using InternLink.Web.Helpers;
using InternLink.Web.Models;
using InternLink.Web.Models.Enums;
using InternLink.Web.Services.Auth;
using InternLink.Web.Services.Email;
using InternLink.Web.ViewModels;

namespace InternLink.Web.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private const string PendingLoginCookie = "InternLink.PendingLogin";
    private static readonly TimeSpan PendingLoginLifetime = TimeSpan.FromMinutes(10);

    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;
    private readonly ApplicationDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly IOtpService _otpService;
    private readonly PendingLoginTokenService _pendingLogin;
    private readonly IWebHostEnvironment _env;
    private readonly DevOtpStore _devOtpStore;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        ApplicationDbContext db,
        IEmailSender emailSender,
        IOtpService otpService,
        PendingLoginTokenService pendingLogin,
        IWebHostEnvironment env,
        DevOtpStore devOtpStore,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _db = db;
        _emailSender = emailSender;
        _otpService = otpService;
        _pendingLogin = pendingLogin;
        _env = env;
        _devOtpStore = devOtpStore;
        _logger = logger;
    }

    // ------------------------------------------------------------------ Register

    [HttpGet]
    public IActionResult Register(string? role = null)
    {
        var model = new RegisterViewModel();
        if (string.Equals(role, "Company", StringComparison.OrdinalIgnoreCase))
        {
            model.Role = RegistrationRole.Company;
        }
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new AppUser
        {
            UserName = model.Email,
            Email = model.Email,
            // Development convenience: skip the email-confirmation step entirely.
            EmailConfirmed = _env.IsDevelopment(),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // EnableRetryOnFailure forbids raw user-initiated transactions; the whole
        // user+profile unit must run inside the execution strategy as one retriable block.
        var strategy = _db.Database.CreateExecutionStrategy();
        IdentityResult? createResult = null;
        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                // User row + linked profile row must both commit or both roll back.
                await using var transaction = await _db.Database.BeginTransactionAsync(ct);

                createResult = await _userManager.CreateAsync(user, model.Password);
                if (!createResult.Succeeded)
                {
                    await transaction.RollbackAsync(ct);
                    return;
                }

                if (model.Role == RegistrationRole.Student)
                {
                    await _userManager.AddToRoleAsync(user, "Student");
                    _db.Students.Add(new Student
                    {
                        UserId = user.Id,
                        FirstName = model.FirstName!.Trim(),
                        LastName = model.LastName!.Trim(),
                        InstitutionalId = model.InstitutionalId!.Trim(),
                        Department = model.Department!.Trim(),
                        CGPA = model.CGPA!.Value,
                        CreatedAt = DateTimeOffset.UtcNow
                    });
                }
                else
                {
                    await _userManager.AddToRoleAsync(user, "Company");
                    _db.Companies.Add(new Company
                    {
                        UserId = user.Id,
                        CompanyName = model.CompanyName!.Trim(),
                        IndustrySector = model.IndustrySector!.Trim(),
                        CorporateWebsite = string.IsNullOrWhiteSpace(model.CorporateWebsite) ? null : model.CorporateWebsite.Trim(),
                        VerificationStatus = VerificationStatus.Pending,
                        CreatedAt = DateTimeOffset.UtcNow
                    });
                }

                await _db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            });
        }
        catch (Exception ex) when (DbExceptionMapper.IsUniqueConstraintViolation(ex))
        {
            _logger.LogWarning(ex, "Registration failed on a unique constraint for {Email}.", model.Email);
            ModelState.AddModelError(string.Empty, "An account with these details already exists.");
            return View(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failed unexpectedly for {Email}.", model.Email);
            ModelState.AddModelError(string.Empty, "Registration could not be completed. Please try again.");
            return View(model);
        }

        // Identity validation failures (duplicate email, weak password) surface as field errors.
        if (createResult is null || !createResult.Succeeded)
        {
            foreach (var error in createResult?.Errors ?? Enumerable.Empty<IdentityError>())
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        // In Development the account is already confirmed — go straight to sign-in.
        if (_env.IsDevelopment())
        {
            TempData["OtpInfo"] = "Account created and auto-confirmed (Development). You can sign in now.";
            return RedirectToAction(nameof(Login));
        }

        // Email confirmation is required before sign-in (SignIn.RequireConfirmedEmail = true).
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var confirmationLink = Url.Action(
            nameof(ConfirmEmail),
            "Account",
            new { userId = user.Id, token },
            Request.Scheme);

        await _emailSender.SendAsync(
            user.Email!,
            "Confirm your InternLink account",
            $"Please confirm your account by clicking this link: <a href=\"{confirmationLink}\">Confirm email</a>",
            ct);

        return RedirectToAction(nameof(RegisterConfirmation));
    }

    [HttpGet]
    public IActionResult RegisterConfirmation()
    {
        return View();
    }

    // ------------------------------------------------------------------ Confirm email

    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(Guid userId, string token)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(token))
        {
            return View(false);
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return View(false);
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);
        return View(result.Succeeded);
    }

    // ------------------------------------------------------------------ Login

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        // Suspension is checked before anything else.
        if (!user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "This account has been suspended. Please contact an administrator.");
            return View(model);
        }

        // Password check only — no auth cookie is issued here. OTP must pass first.
        var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "This account is locked due to multiple failed attempts. Try again in 15 minutes.");
            return View(model);
        }

        if (result.IsNotAllowed)
        {
            ModelState.AddModelError(string.Empty, "You must confirm your email address before signing in.");
            return View(model);
        }

        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        // Second factor: issue an email OTP and hand off to VerifyOtp. SignInAsync happens only after OTP passes.
        await _otpService.SendAsync(user.Id, user.Email!, ct);
        var pendingToken = _pendingLogin.Create(user.Id, model.RememberMe);
        Response.Cookies.Append(PendingLoginCookie, pendingToken, BuildPendingCookieOptions());

        return RedirectToAction(nameof(VerifyOtp), new { returnUrl = model.ReturnUrl });
    }

    // ------------------------------------------------------------------ OTP second factor

    [HttpGet]
    public async Task<IActionResult> VerifyOtp(string? returnUrl = null)
    {
        if (!_pendingLogin.TryRead(Request.Cookies[PendingLoginCookie], PendingLoginLifetime, out var userId, out _))
        {
            return RedirectToAction(nameof(Login));
        }

        var model = new VerifyOtpViewModel { ReturnUrl = returnUrl };

        // Development convenience: pre-fill the code captured by DevEmailSender.
        if (_env.IsDevelopment())
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user?.Email is not null)
            {
                model.Code = _devOtpStore.Get(user.Email) ?? string.Empty;
            }
        }

        return View(model);
    }

    [HttpPost]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> VerifyOtp(VerifyOtpViewModel model, CancellationToken ct)
    {
        if (!_pendingLogin.TryRead(Request.Cookies[PendingLoginCookie], PendingLoginLifetime, out var userId, out var remember))
        {
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _otpService.VerifyAsync(userId, model.Code, ct);
        if (result != OtpVerifyResult.Success)
        {
            // Generic message: never reveal whether the code was wrong vs expired.
            ModelState.AddModelError(string.Empty, "Invalid or expired code.");
            return View(model);
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive)
        {
            Response.Cookies.Delete(PendingLoginCookie);
            return RedirectToAction(nameof(Login));
        }

        Response.Cookies.Delete(PendingLoginCookie);
        await _signInManager.SignInAsync(user, isPersistent: remember);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        var roles = await _userManager.GetRolesAsync(user);
        return RedirectToRoleDashboard(roles);
    }

    [HttpPost]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendOtp(string? returnUrl, CancellationToken ct)
    {
        if (!_pendingLogin.TryRead(Request.Cookies[PendingLoginCookie], PendingLoginLifetime, out var userId, out _))
        {
            return RedirectToAction(nameof(Login));
        }

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        var model = new VerifyOtpViewModel { ReturnUrl = returnUrl };
        var result = await _otpService.ResendAsync(userId, user.Email!, ct);
        if (result == OtpResendResult.TooSoon)
        {
            ModelState.AddModelError(string.Empty, "Please wait 30 seconds before requesting another code.");
        }
        else if (result == OtpResendResult.Sent)
        {
            TempData["OtpInfo"] = "A new verification code has been sent.";
        }
        else
        {
            return RedirectToAction(nameof(Login));
        }

        return View(nameof(VerifyOtp), model);
    }

    // ------------------------------------------------------------------ Logout

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    // ------------------------------------------------------------------ Access denied

    [HttpGet]
    public IActionResult Denied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    private CookieOptions BuildPendingCookieOptions() => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Secure = Request.IsHttps,
        IsEssential = true,
        Expires = DateTimeOffset.UtcNow.Add(PendingLoginLifetime)
    };

    private IActionResult RedirectToRoleDashboard(IList<string> roles)
    {
        if (roles.Contains("Admin"))
        {
            return RedirectToAction("Index", "Home", new { area = "Admin" });
        }
        if (roles.Contains("Company"))
        {
            return RedirectToAction("Index", "Home", new { area = "Company" });
        }
        if (roles.Contains("Counselor"))
        {
            return RedirectToAction("Index", "Home", new { area = "Counselor" });
        }
        if (roles.Contains("Student"))
        {
            return RedirectToAction("Index", "Home", new { area = "Student" });
        }
        return RedirectToAction("Index", "Home");
    }
}

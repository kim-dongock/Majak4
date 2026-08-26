using MajakServer.Repositories.MySQL;
using MajakServer.Services;
using MajakServer.Infrastructure;

namespace MajakServer.Endpoints;

internal static class AdminEndpoints
{
    internal static void MapAdminEndpoints(this WebApplication app)
    {
        app.MapPost("/api/admin/auth/google", async (
            HttpContext context,
            AdminAuthService adminAuth) =>
        {
            var body = await context.Request.ReadFromJsonAsync<GoogleLoginRequest>();
            if (string.IsNullOrWhiteSpace(body?.IdToken))
                return Results.BadRequest(new { error = "idToken required" });

            var result = await adminAuth.LoginWithGoogleAsync(body.IdToken);
            if (!result.Success) return Results.Unauthorized();
            return Results.Ok(new { token = result.Token, adminNo = result.AdminNo, email = result.Email, role = result.Role });
        }).RequireCors("AdminPolicy");

        app.MapGet("/api/admin/dashboard", async (
            HttpContext context,
            AdminAuthService adminAuth,
            AdminRepository adminRepository) =>
        {
            if (RequireAdminAuth(context, adminAuth) is { } error) return error;
            return Results.Ok(await adminRepository.GetDashboardStatsAsync());
        }).RequireCors("AdminPolicy");

        app.MapGet("/api/admin/users", async (
            HttpContext context,
            AdminAuthService adminAuth,
            AdminRepository adminRepository,
            string? keyword,
            int offset = 0,
            int limit = 30) =>
        {
            if (RequireAdminAuth(context, adminAuth) is { } error) return error;
            if (limit > 100) limit = 100;
            return Results.Ok(await adminRepository.SearchPlayersAsync(keyword, offset, limit));
        }).RequireCors("AdminPolicy");

        app.MapGet("/api/admin/users/{memberNo}", async (
            ulong memberNo,
            HttpContext context,
            AdminAuthService adminAuth,
            AdminRepository adminRepository) =>
        {
            if (RequireAdminAuth(context, adminAuth) is { } error) return error;
            var player = await adminRepository.GetPlayerDetailAsync(memberNo);
            return player is null ? Results.NotFound() : Results.Ok(player);
        }).RequireCors("AdminPolicy");

        app.MapGet("/api/admin/accounts", async (HttpContext context, AdminAuthService adminAuth, AdminRepository adminRepository) =>
        {
            if (RequireAdminAuth(context, adminAuth, "super_admin") is { } error) return error;
            return Results.Ok(await adminRepository.GetAdminAccountsAsync());
        }).RequireCors("AdminPolicy");

        app.MapPost("/api/admin/accounts", async (HttpContext context, AdminAuthService adminAuth, AdminRepository adminRepository) =>
        {
            if (RequireAdminAuth(context, adminAuth, "super_admin") is { } error) return error;
            var body = await context.Request.ReadFromJsonAsync<AdminAccountRequest>();
            if (string.IsNullOrWhiteSpace(body?.Email) || body.Role is not ("super_admin" or "operator" or "viewer"))
                return Results.BadRequest(new { error = "email and valid role required" });
            return Results.Ok(await adminRepository.UpsertAdminAccountAsync(body.Email, body.Role));
        }).RequireCors("AdminPolicy");

        app.MapDelete("/api/admin/accounts/{email}", async (string email, HttpContext context, AdminAuthService adminAuth, AdminRepository adminRepository) =>
        {
            if (RequireAdminAuth(context, adminAuth, "super_admin") is { } error) return error;
            await adminRepository.SetAdminAccountActiveAsync(email, false);
            return Results.Ok(new { disabled = true });
        }).RequireCors("AdminPolicy");

        app.MapGet("/api/admin/users/pending", async (
            HttpContext context,
            AdminAuthService adminAuth,
            AdminRepository adminRepository,
            int offset = 0,
            int limit = 50) =>
        {
            if (RequireAdminAuth(context, adminAuth) is { } error) return error;
            var items = await adminRepository.GetPendingPlayersAsync(offset, limit);
            var total = await adminRepository.CountPendingPlayersAsync();
            return Results.Ok(new { total, offset, limit, items });
        }).RequireCors("AdminPolicy");

        app.MapPost("/api/admin/users/{memberNo}/approve", async (ulong memberNo, HttpContext context, AdminAuthService adminAuth, AdminRepository adminRepository) =>
        {
            if (RequireAdminAuth(context, adminAuth) is { } error) return error;
            var adminNo = GetAdminNoClaim(context, adminAuth);
            if (adminNo is null) return Results.Unauthorized();
            return await adminRepository.ApprovePlayerAsync(memberNo, adminNo.Value)
                ? Results.Ok(new { approved = true })
                : Results.NotFound(new { error = "NOT_PENDING" });
        }).RequireCors("AdminPolicy");

        app.MapPost("/api/admin/users/{memberNo}/suspend", async (ulong memberNo, HttpContext context, AdminAuthService adminAuth, AdminRepository adminRepository) =>
        {
            if (RequireAdminAuth(context, adminAuth) is { } error) return error;
            var body = await context.Request.ReadFromJsonAsync<SuspendRequest>();
            var adminNo = GetAdminNoClaim(context, adminAuth);
            if (adminNo is null) return Results.Unauthorized();
            return await adminRepository.SuspendPlayerAsync(memberNo, adminNo.Value, body?.Reason ?? "")
                ? Results.Ok(new { suspended = true })
                : Results.NotFound(new { error = "NOT_FOUND" });
        }).RequireCors("AdminPolicy");

        app.MapPost("/api/admin/users/{memberNo}/unsuspend", async (ulong memberNo, HttpContext context, AdminAuthService adminAuth, AdminRepository adminRepository) =>
        {
            if (RequireAdminAuth(context, adminAuth) is { } error) return error;
            var adminNo = GetAdminNoClaim(context, adminAuth);
            if (adminNo is null) return Results.Unauthorized();
            return await adminRepository.UnsuspendPlayerAsync(memberNo, adminNo.Value)
                ? Results.Ok(new { unsuspended = true })
                : Results.NotFound(new { error = "NOT_FOUND" });
        }).RequireCors("AdminPolicy");

            app.MapGet("/api/admin/announcements", async (HttpContext context, AdminAuthService adminAuth, GameAnnouncementRepository announcements) =>
            {
                if (RequireAdminAuth(context, adminAuth) is { } error) return error;
                return Results.Ok(await announcements.GetAllAsync());
            }).RequireCors("AdminPolicy");

            app.MapPost("/api/admin/announcements", async (HttpContext context, AdminAuthService adminAuth, GameAnnouncementRepository announcements) =>
            {
                if (RequireAdminAuth(context, adminAuth, "super_admin") is { } error) return error;
                var body = await context.Request.ReadFromJsonAsync<GameAnnouncementRequest>();
                if (!IsValidAnnouncement(body)) return Results.BadRequest(new { error = "title and body are required (title <= 120, body <= 10000)" });
                return Results.Ok(await announcements.CreateAsync(body!.Title.Trim(), body.Body.Trim(), body.IsPublished, body.IsStartup));
            }).RequireCors("AdminPolicy");

            app.MapPut("/api/admin/announcements/{announcementId:long}", async (long announcementId, HttpContext context, AdminAuthService adminAuth, GameAnnouncementRepository announcements) =>
            {
                if (RequireAdminAuth(context, adminAuth, "super_admin") is { } error) return error;
                var body = await context.Request.ReadFromJsonAsync<GameAnnouncementRequest>();
                if (!IsValidAnnouncement(body)) return Results.BadRequest(new { error = "title and body are required (title <= 120, body <= 10000)" });
                var updated = await announcements.UpdateAsync(announcementId, body!.Title.Trim(), body.Body.Trim(), body.IsPublished, body.IsStartup);
                return updated is null ? Results.NotFound() : Results.Ok(updated);
            }).RequireCors("AdminPolicy");

            app.MapDelete("/api/admin/announcements/{announcementId:long}", async (long announcementId, HttpContext context, AdminAuthService adminAuth, GameAnnouncementRepository announcements) =>
            {
                if (RequireAdminAuth(context, adminAuth, "super_admin") is { } error) return error;
                return await announcements.DeleteAsync(announcementId) ? Results.NoContent() : Results.NotFound();
            }).RequireCors("AdminPolicy");

            app.MapPost("/api/admin/notice", async (HttpContext context, GradeRankBackgroundService noticeService) =>
            {
                var body = await context.Request.ReadFromJsonAsync<NoticeRequest>();
                if (body is null || string.IsNullOrWhiteSpace(body.Message))
                    return Results.BadRequest(new { error = "message required" });
                await noticeService.SendNoticeToAllAsync(body.Message, body.Color);
                return Results.Ok(new { sent = true });
            });

            app.MapGet("/api/admin/cash/products", async (HttpContext context, AdminAuthService adminAuth, AdminRepository adminRepository) =>
            {
                if (RequireAdminAuth(context, adminAuth) is { } error) return error;
                return Results.Ok(await adminRepository.GetCashProductsAsync());
            }).RequireCors("AdminPolicy");

            app.MapPut("/api/admin/cash/products/{productId}", async (string productId, HttpContext context, AdminAuthService adminAuth, AdminRepository adminRepository) =>
            {
                if (RequireAdminAuth(context, adminAuth, "super_admin") is { } error) return error;
                var body = await context.Request.ReadFromJsonAsync<CashProduct>();
                if (body is null || body.ProductId != productId)
                    return Results.BadRequest(new { error = "productId mismatch" });
                await adminRepository.UpdateCashProductAsync(body);
                return Results.Ok(new { updated = true });
            }).RequireCors("AdminPolicy");

            app.MapGet("/api/admin/cash/revenue", async (HttpContext context, AdminAuthService adminAuth, AdminRepository adminRepository, int days = 30) =>
            {
                if (RequireAdminAuth(context, adminAuth) is { } error) return error;
                if (days > 365) days = 365;
                return Results.Ok(await adminRepository.GetDailyRevenueAsync(days));
            }).RequireCors("AdminPolicy");

            app.MapGet("/api/admin/economy-policy", async (HttpContext context, AdminAuthService adminAuth, IGameEconomyPolicyService economyPolicy) =>
            {
                if (RequireAdminAuth(context, adminAuth, "super_admin") is { } error) return error;
                return Results.Ok(await economyPolicy.GetCurrentAsync());
            }).RequireCors("AdminPolicy");

            app.MapPut("/api/admin/economy-policy", async (HttpContext context, AdminAuthService adminAuth, EconomyPolicyRepository economyPolicyRepository, LogDbContext logDb) =>
            {
                if (RequireAdminAuth(context, adminAuth, "super_admin") is { } error) return error;
                var body = await context.Request.ReadFromJsonAsync<EconomyPolicyRequest>();
                if (body is null || body.InitialGp < 0 || body.FreeReplenishTargetGp < 0 || body.FreeReplenishDailyLimit < 0 || body.FreeReplenishDailyLimit > 100)
                    return Results.BadRequest(new { error = "GP values must be non-negative and daily limit must be between 0 and 100" });
                var operatorNo = GetAdminNoClaim(context, adminAuth);
                if (operatorNo is null) return Results.Unauthorized();
                var before = await economyPolicyRepository.GetAsync();
                var updated = await economyPolicyRepository.UpdateAsync(body.InitialGp, body.FreeReplenishTargetGp, body.FreeReplenishDailyLimit);
                var role = adminAuth.ValidateJwt(context.Request.Headers.Authorization.FirstOrDefault()? ["Bearer ".Length..].Trim() ?? string.Empty)?.FindFirst("role")?.Value ?? "super_admin";
                await using var logConnection = await logDb.CreateConnectionAsync();
                await using var logCommand = new MySqlConnector.MySqlCommand(@"
                    INSERT INTO admin_operation_log
                        (operator_no, operator_role, action, target_type, target_id, payload_before, payload_after, client_ip, occurred_at)
                    VALUES (@operatorNo, @role, 'UPDATE', 'GAME_ECONOMY_POLICY', '1', @before, @after, @ip, CURRENT_TIMESTAMP(3))", logConnection);
                logCommand.Parameters.AddWithValue("@operatorNo", operatorNo.Value);
                logCommand.Parameters.AddWithValue("@role", role);
                logCommand.Parameters.AddWithValue("@before", (object?)System.Text.Json.JsonSerializer.Serialize(before) ?? DBNull.Value);
                logCommand.Parameters.AddWithValue("@after", System.Text.Json.JsonSerializer.Serialize(updated));
                logCommand.Parameters.AddWithValue("@ip", (object?)context.Connection.RemoteIpAddress?.ToString() ?? DBNull.Value);
                await logCommand.ExecuteNonQueryAsync();
                return Results.Ok(updated);
            }).RequireCors("AdminPolicy");

            app.MapPost("/api/admin/cash/adjust", async (
                HttpContext context,
                AdminAuthService adminAuth,
                AdminRepository adminRepository,
                LogDbContext logDb) =>
            {
                if (RequireAdminAuth(context, adminAuth, "operator") is { } error) return error;

                var body = await context.Request.ReadFromJsonAsync<CashAdjustRequest>();
                if (body is null || body.MemberNo == 0 || body.Amount == 0)
                    return Results.BadRequest(new { error = "memberNo and non-zero amount required" });
                if (string.IsNullOrWhiteSpace(body.Memo))
                    return Results.BadRequest(new { error = "memo required for admin Majak Cash adjustment" });

                var player = await adminRepository.GetPlayerDetailAsync(body.MemberNo);
                if (player is null) return Results.NotFound(new { error = "player not found" });

                var adjustment = await adminRepository.AdjustCashAsync(body.MemberNo, body.Amount);
                var operatorNo = GetAdminNoClaim(context, adminAuth);
                if (operatorNo is null) return Results.Unauthorized();

                await using var logConnection = await logDb.CreateConnectionAsync();
                await using var logCommand = new MySqlConnector.MySqlCommand(@"
                    INSERT INTO cash_transaction_log
                        (member_no, event_type, amount, balance_before, balance_after,
                         paid_amount, free_amount, paid_before, paid_after, free_before, free_after,
                         ref_id, memo, operator_no, client_ip, occurred_at)
                    VALUES
                        (@memberNo, @eventType, @amount, @before, @after,
                         @paidAmount, @freeAmount, @paidBefore, @paidAfter, @freeBefore, @freeAfter,
                         NULL, @memo, @opNo, @ip, CURRENT_TIMESTAMP(3))", logConnection);
                logCommand.Parameters.AddWithValue("@memberNo", body.MemberNo);
                logCommand.Parameters.AddWithValue("@eventType", body.Amount > 0 ? "ADMIN_GRANT_FREE" : "ADMIN_DEDUCT");
                logCommand.Parameters.AddWithValue("@amount", body.Amount);
                logCommand.Parameters.AddWithValue("@before", adjustment.TotalBefore);
                logCommand.Parameters.AddWithValue("@after", adjustment.TotalAfter);
                logCommand.Parameters.AddWithValue("@paidAmount", adjustment.PaidAfter - adjustment.PaidBefore);
                logCommand.Parameters.AddWithValue("@freeAmount", adjustment.FreeAfter - adjustment.FreeBefore);
                logCommand.Parameters.AddWithValue("@paidBefore", adjustment.PaidBefore);
                logCommand.Parameters.AddWithValue("@paidAfter", adjustment.PaidAfter);
                logCommand.Parameters.AddWithValue("@freeBefore", adjustment.FreeBefore);
                logCommand.Parameters.AddWithValue("@freeAfter", adjustment.FreeAfter);
                logCommand.Parameters.AddWithValue("@memo", body.Memo);
                logCommand.Parameters.AddWithValue("@opNo", operatorNo.Value);
                logCommand.Parameters.AddWithValue("@ip", (object?)context.Connection.RemoteIpAddress?.ToString() ?? DBNull.Value);
                await logCommand.ExecuteNonQueryAsync();

                var role = adminAuth.ValidateJwt(context.Request.Headers.Authorization.FirstOrDefault()?["Bearer ".Length..].Trim() ?? string.Empty)
                    ?.FindFirst("role")?.Value ?? "operator";
                await using var auditCommand = new MySqlConnector.MySqlCommand(@"
                    INSERT INTO admin_operation_log
                        (operator_no, operator_role, action, target_type, target_id, payload_before, payload_after, client_ip, occurred_at)
                    VALUES (@operatorNo, @role, 'CASH_ADJUST', 'PLAYER_WALLET', @memberNo, @before, @after, @ip, CURRENT_TIMESTAMP(3))", logConnection);
                auditCommand.Parameters.AddWithValue("@operatorNo", operatorNo.Value);
                auditCommand.Parameters.AddWithValue("@role", role);
                auditCommand.Parameters.AddWithValue("@memberNo", body.MemberNo.ToString(System.Globalization.CultureInfo.InvariantCulture));
                auditCommand.Parameters.AddWithValue("@before", System.Text.Json.JsonSerializer.Serialize(new
                {
                    cashCount = adjustment.TotalBefore,
                    paidCashCount = adjustment.PaidBefore,
                    freeCashCount = adjustment.FreeBefore,
                    memo = body.Memo,
                }));
                auditCommand.Parameters.AddWithValue("@after", System.Text.Json.JsonSerializer.Serialize(new
                {
                    cashCount = adjustment.TotalAfter,
                    paidCashCount = adjustment.PaidAfter,
                    freeCashCount = adjustment.FreeAfter,
                    memo = body.Memo,
                }));
                auditCommand.Parameters.AddWithValue("@ip", (object?)context.Connection.RemoteIpAddress?.ToString() ?? DBNull.Value);
                await auditCommand.ExecuteNonQueryAsync();

                return Results.Ok(new
                {
                    memberNo = body.MemberNo,
                    balanceBefore = adjustment.TotalBefore,
                    balanceAfter = adjustment.TotalAfter,
                    paidCashBefore = adjustment.PaidBefore,
                    paidCashAfter = adjustment.PaidAfter,
                    freeCashBefore = adjustment.FreeBefore,
                    freeCashAfter = adjustment.FreeAfter,
                });
            }).RequireCors("AdminPolicy");

            app.MapPost("/api/admin/currency/adjust", async (
                HttpContext context,
                AdminAuthService adminAuth,
                AdminRepository adminRepository,
                LogDbContext logDb,
                LogRepository logRepository,
                PlayerSessionService sessions,
                RatingService ratingService) =>
            {
                if (RequireAdminAuth(context, adminAuth, "operator") is { } error) return error;

                var body = await context.Request.ReadFromJsonAsync<CurrencyAdjustRequest>();
                var currency = body?.Currency?.Trim().ToLowerInvariant() ?? string.Empty;
                if (body is null || body.MemberNo == 0 || body.Amount == 0 || currency is not ("gp" or "mp" or "dragon_orb"))
                    return Results.BadRequest(new { error = "memberNo, currency (gp|mp|dragon_orb), and non-zero amount required" });
                if (string.IsNullOrWhiteSpace(body.Memo))
                    return Results.BadRequest(new { error = "memo required for admin currency adjustment" });
                if (currency == "mp" && (body.Amount < int.MinValue || body.Amount > int.MaxValue))
                    return Results.BadRequest(new { error = "MP adjustment is out of range" });

                var player = await adminRepository.GetPlayerDetailAsync(body.MemberNo);
                if (player is null) return Results.NotFound(new { error = "player not found" });
                var operatorNo = GetAdminNoClaim(context, adminAuth);
                if (operatorNo is null) return Results.Unauthorized();

                long balanceBefore;
                long balanceAfter;
                CashBalanceAdjustment? cashAdjustment = null;
                string eventTitle;
                string eventCode;
                if (currency == "mp")
                {
                    cashAdjustment = await adminRepository.AdjustCashAsync(body.MemberNo, checked((int)body.Amount));
                    balanceBefore = cashAdjustment.TotalBefore;
                    balanceAfter = cashAdjustment.TotalAfter;
                    eventTitle = body.Amount > 0 ? "管理者MP支給" : "管理者MP回収";
                    eventCode = body.Amount > 0 ? "ADMIN_GRANT_FREE" : "ADMIN_DEDUCT";
                }
                else
                {
                    var adjustment = await adminRepository.AdjustGameCurrencyAsync(body.MemberNo, currency, body.Amount);
                    balanceBefore = adjustment.BalanceBefore;
                    balanceAfter = adjustment.BalanceAfter;
                    eventTitle = currency == "gp"
                        ? body.Amount > 0 ? "管理者GP支給" : "管理者GP回収"
                        : body.Amount > 0 ? "管理者龍珠支給" : "管理者龍珠回収";
                    eventCode = currency == "gp" ? "ADMIN_GP_ADJUST" : "DRAGON_ORB_ADMIN_ADJUST";
                    await logRepository.InsertGameMoneyHistAsync(
                        body.MemberNo.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        eventCode,
                        body.Amount,
                        balanceBefore,
                        balanceAfter,
                        context.Connection.RemoteIpAddress?.ToString() ?? string.Empty,
                        eventTitle);
                }

                await using var logConnection = await logDb.CreateConnectionAsync();
                if (cashAdjustment is not null)
                {
                    await using var cashCommand = new MySqlConnector.MySqlCommand(@"
                        INSERT INTO cash_transaction_log
                            (member_no, event_type, amount, balance_before, balance_after,
                             paid_amount, free_amount, paid_before, paid_after, free_before, free_after,
                             ref_id, memo, operator_no, client_ip, occurred_at)
                        VALUES
                            (@memberNo, @eventType, @amount, @before, @after,
                             @paidAmount, @freeAmount, @paidBefore, @paidAfter, @freeBefore, @freeAfter,
                             NULL, @memo, @operatorNo, @ip, CURRENT_TIMESTAMP(3))", logConnection);
                    cashCommand.Parameters.AddWithValue("@memberNo", body.MemberNo);
                    cashCommand.Parameters.AddWithValue("@eventType", eventCode);
                    cashCommand.Parameters.AddWithValue("@amount", body.Amount);
                    cashCommand.Parameters.AddWithValue("@before", balanceBefore);
                    cashCommand.Parameters.AddWithValue("@after", balanceAfter);
                    cashCommand.Parameters.AddWithValue("@paidAmount", cashAdjustment.PaidAfter - cashAdjustment.PaidBefore);
                    cashCommand.Parameters.AddWithValue("@freeAmount", cashAdjustment.FreeAfter - cashAdjustment.FreeBefore);
                    cashCommand.Parameters.AddWithValue("@paidBefore", cashAdjustment.PaidBefore);
                    cashCommand.Parameters.AddWithValue("@paidAfter", cashAdjustment.PaidAfter);
                    cashCommand.Parameters.AddWithValue("@freeBefore", cashAdjustment.FreeBefore);
                    cashCommand.Parameters.AddWithValue("@freeAfter", cashAdjustment.FreeAfter);
                    cashCommand.Parameters.AddWithValue("@memo", body.Memo);
                    cashCommand.Parameters.AddWithValue("@operatorNo", operatorNo.Value);
                    cashCommand.Parameters.AddWithValue("@ip", (object?)context.Connection.RemoteIpAddress?.ToString() ?? DBNull.Value);
                    await cashCommand.ExecuteNonQueryAsync();
                }

                var role = adminAuth.ValidateJwt(context.Request.Headers.Authorization.FirstOrDefault()?["Bearer ".Length..].Trim() ?? string.Empty)
                    ?.FindFirst("role")?.Value ?? "operator";
                await using var auditCommand = new MySqlConnector.MySqlCommand(@"
                    INSERT INTO admin_operation_log
                        (operator_no, operator_role, action, target_type, target_id, payload_before, payload_after, client_ip, occurred_at)
                    VALUES (@operatorNo, @role, 'CURRENCY_ADJUST', 'PLAYER_WALLET', @memberNo, @before, @after, @ip, CURRENT_TIMESTAMP(3))", logConnection);
                auditCommand.Parameters.AddWithValue("@operatorNo", operatorNo.Value);
                auditCommand.Parameters.AddWithValue("@role", role);
                auditCommand.Parameters.AddWithValue("@memberNo", body.MemberNo.ToString(System.Globalization.CultureInfo.InvariantCulture));
                auditCommand.Parameters.AddWithValue("@before", System.Text.Json.JsonSerializer.Serialize(new { currency, balance = balanceBefore, memo = body.Memo }));
                auditCommand.Parameters.AddWithValue("@after", System.Text.Json.JsonSerializer.Serialize(new { currency, balance = balanceAfter, memo = body.Memo }));
                auditCommand.Parameters.AddWithValue("@ip", (object?)context.Connection.RemoteIpAddress?.ToString() ?? DBNull.Value);
                await auditCommand.ExecuteNonQueryAsync();

                var activePlayer = sessions.GetByMember(body.MemberNo.ToString(System.Globalization.CultureInfo.InvariantCulture));
                if (activePlayer is not null)
                {
                    if (currency == "gp")
                    {
                        activePlayer.GamMoney = balanceAfter;
                        ratingService.UpdatePlayerLevel(activePlayer);
                    }
                    else if (currency == "dragon_orb")
                    {
                        activePlayer.GemCount = checked((int)balanceAfter);
                    }
                    else
                    {
                        activePlayer.CashCount = checked((int)balanceAfter);
                        activePlayer.PaidCashCount = cashAdjustment!.PaidAfter;
                        activePlayer.FreeCashCount = cashAdjustment.FreeAfter;
                    }
                }

                return Results.Ok(new { memberNo = body.MemberNo, currency, amount = body.Amount, balanceBefore, balanceAfter });
            }).RequireCors("AdminPolicy");
    }

    private static IResult? RequireAdminAuth(HttpContext context, AdminAuthService auth, string? requiredRole = null)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (authHeader is null || !authHeader.StartsWith("Bearer ")) return Results.Unauthorized();
        var principal = auth.ValidateJwt(authHeader["Bearer ".Length..].Trim());
        if (principal is null) return Results.Unauthorized();
        if (requiredRole is null) return null;

        var role = principal.FindFirst("role")?.Value
            ?? principal.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        return string.Equals(role, requiredRole, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "super_admin", StringComparison.OrdinalIgnoreCase)
            ? null
            : Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    private static ulong? GetAdminNoClaim(HttpContext context, AdminAuthService auth)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (authHeader is null || !authHeader.StartsWith("Bearer ")) return null;
        var adminNo = auth.ValidateJwt(authHeader["Bearer ".Length..].Trim())?.FindFirst("admin_no")?.Value;
        return ulong.TryParse(adminNo, out var parsed) ? parsed : null;
    }

    private static bool IsValidAnnouncement(GameAnnouncementRequest? body)
        => body is not null && !string.IsNullOrWhiteSpace(body.Title) && !string.IsNullOrWhiteSpace(body.Body)
            && body.Title.Length <= 120 && body.Body.Length <= 10000;
}
using MajakServer.Repositories.MySQL;
using MajakServer.Services;

namespace MajakServer.Endpoints;

internal static class PlayerEndpoints
{
    internal static void MapPlayerEndpoints(this WebApplication app)
    {
        app.MapPatch("/api/player/account-profile", async (
            HttpContext context,
            AccountProfileUpdateRequest? body,
            GamePlayerRepository gamePlayers,
            PlayerSessionService sessions,
            GameAuthTokenService gameAuth) =>
        {
            var auth = gameAuth.Validate(context.Request.Headers.Authorization.FirstOrDefault());
            if (auth is null) return Results.Unauthorized();
            if (body?.BirthYear is null || body.BirthYear < 1900 || body.BirthYear > DateTime.UtcNow.Year)
                return Results.BadRequest(new { error = "INVALID_BIRTH_YEAR" });

            var account = await gamePlayers.GetAccountAsync(auth.MemberNo);
            if (account is null) return Results.NotFound(new { error = "PLAYER_ACCOUNT_NOT_FOUND" });
            if (string.IsNullOrWhiteSpace(body.AvatarId) || !AvatarCatalog.IsValid(account.SexCode, body.AvatarId))
                return Results.BadRequest(new { error = "INVALID_AVATAR" });
            if (!AuthEndpoints.IsValidUserColor(body.UserColor))
                return Results.BadRequest(new { error = "INVALID_USER_COLOR" });

            if (!await gamePlayers.UpdateAccountProfileAsync(auth.MemberNo, (ushort)body.BirthYear.Value, body.AvatarId, body.UserColor!))
                return Results.NotFound(new { error = "PLAYER_ACCOUNT_NOT_FOUND" });

            var activePlayer = sessions.GetByMember(auth.MemberNo);
            if (activePlayer is not null)
            {
                activePlayer.AvatarId = body.AvatarId;
                activePlayer.UserColor = body.UserColor!;
                activePlayer.Age = Math.Max(0, DateTime.UtcNow.Year - body.BirthYear.Value);
            }

            return Results.Ok(new { birthYear = body.BirthYear.Value, avatarId = body.AvatarId, userColor = body.UserColor });
        });

        app.MapGet("/api/player/profile", async (
            HttpContext context,
            PlayerRepository playerRepository,
            RatingService ratingService,
            GameMoneyService moneyService,
            GameAuthTokenService gameAuth) =>
        {
            var auth = gameAuth.Validate(context.Request.Headers.Authorization.FirstOrDefault());
            if (auth is null) return Results.Unauthorized();

            var player = new MajakServer.Models.Player.MajakPlayer { MemberNo = auth.MemberNo };
            if (!await playerRepository.ExistsCommonRatAsync(auth.MemberNo))
                await moneyService.CreateCommonRatWithConfiguredMoneyHistAsync(auth.MemberNo, "");

            await playerRepository.LoadCommonRatAsync(player);
            await playerRepository.LoadGradeRatAsync(player);
            ratingService.UpdatePlayerLevel(player);

            return Results.Ok(new
            {
                gamMoney = player.GamMoney,
                slevel = player.SLevel,
                nlevel = player.NLevel,
                gradeLevel = player.GradeRecord.Grade,
                rating = player.Rating,
                trickTitle = player.TrickTitle,
                majakTitle = player.MajakTitle,
                gemCount = player.GemCount,
                cashCount = player.CashCount,
            });
        });

        app.MapGet("/api/player/currency-history", async (
            HttpContext context,
            LogRepository logRepository,
            GameAuthTokenService gameAuth,
            string? currency,
            DateOnly? from,
            DateOnly? to,
            string? cursor,
            int limit = 30) =>
        {
            var auth = gameAuth.Validate(context.Request.Headers.Authorization.FirstOrDefault());
            if (auth is null) return Results.Unauthorized();

            var selectedCurrency = currency?.ToLowerInvariant() ?? "all";
            if (selectedCurrency is not ("all" or "gp" or "mp" or "dragon_orb"))
                return Results.BadRequest(new { error = "INVALID_CURRENCY" });
            if (!TryDecodeCurrencyHistoryCursor(cursor, out var decodedCursor))
                return Results.BadRequest(new { error = "INVALID_CURSOR" });

            var today = DateOnly.FromDateTime(DateTime.Today);
            var startDate = from ?? today.AddDays(-6);
            var endDate = to ?? today;
            if (startDate > endDate || endDate.DayNumber - startDate.DayNumber > 366)
                return Results.BadRequest(new { error = "INVALID_DATE_RANGE" });

            var page = await logRepository.GetCurrencyHistoryAsync(
                auth.MemberNo,
                selectedCurrency,
                startDate.ToDateTime(TimeOnly.MinValue),
                endDate.AddDays(1).ToDateTime(TimeOnly.MinValue),
                decodedCursor,
                Math.Clamp(limit, 1, 100));
            var nextCursor = page.HasMore && page.Items.Count > 0
                ? EncodeCurrencyHistoryCursor(new CurrencyHistoryCursor(
                    page.Items[^1].OccurredAt,
                    page.Items[^1].Source,
                    page.Items[^1].Id))
                : null;
            return Results.Ok(new { items = page.Items, nextCursor, hasMore = page.HasMore });
        });

        app.MapGet("/api/player/collection", async (
            HttpContext context,
            PlayerRepository playerRepository,
            TitleService titleService,
            PlayerSessionService sessions,
            GameAuthTokenService gameAuth) =>
        {
            var auth = gameAuth.Validate(context.Request.Headers.Authorization.FirstOrDefault());
            if (auth is null) return Results.Unauthorized();

            var activePlayer = sessions.GetByMember(auth.MemberNo);
            var player = activePlayer ?? new MajakServer.Models.Player.MajakPlayer { MemberNo = auth.MemberNo };
            if (activePlayer is null && !await playerRepository.LoadCommonRatAsync(player))
                return Results.NotFound(new { error = "PLAYER_PROFILE_NOT_FOUND" });

            var (majakTitles, titleTitles, trickTitles) = await titleService.GetCollectionAsync(player);
            return Results.Ok(new { majakTitles, titleTitles, trickTitles, equippedMajakTitle = player.MajakTitle, equippedTrickTitle = player.TrickTitle });
        });

        app.MapPost("/api/player/collection/equip", async (
            HttpContext context,
            CollectionEquipRequest? body,
            PlayerRepository playerRepository,
            TitleService titleService,
            PlayerSessionService sessions,
            GameAuthTokenService gameAuth) =>
        {
            var auth = gameAuth.Validate(context.Request.Headers.Authorization.FirstOrDefault());
            if (auth is null) return Results.Unauthorized();
            string category = body?.Category ?? "";
            if (category is not ("majak" or "title" or "trick"))
                return Results.BadRequest(new { error = "INVALID_TITLE_CATEGORY" });

            var activePlayer = sessions.GetByMember(auth.MemberNo);
            var player = activePlayer ?? new MajakServer.Models.Player.MajakPlayer { MemberNo = auth.MemberNo };
            if (activePlayer is null && !await playerRepository.LoadCommonRatAsync(player))
                return Results.NotFound(new { error = "PLAYER_PROFILE_NOT_FOUND" });
            if (!await titleService.EquipOwnedTitleAsync(player, category, body?.TitleId))
                return Results.BadRequest(new { error = "TITLE_NOT_OWNED" });

            var (majakTitles, titleTitles, trickTitles) = await titleService.GetCollectionAsync(player);
            return Results.Ok(new { majakTitles, titleTitles, trickTitles, equippedMajakTitle = player.MajakTitle, equippedTrickTitle = player.TrickTitle });
        });

        app.MapGet("/api/player/paifu", async (
            HttpContext context,
            PaifuFileService paifuFiles,
            GameAuthTokenService gameAuth,
            DateTime? from,
            DateTime? to,
            string? room,
            string? member,
            string? result,
            string? matchKind,
            int limit = 100) =>
        {
            var auth = gameAuth.Validate(context.Request.Headers.Authorization.FirstOrDefault());
            if (auth is null) return Results.Unauthorized();
            var kind = matchKind?.ToLowerInvariant() switch
            {
                "normal" => PaifuMatchKind.Normal,
                "tournament" => PaifuMatchKind.Tournament,
                _ => PaifuMatchKind.All,
            };
            var query = new PaifuArchiveQuery(
                from?.Date,
                to?.Date.AddDays(1),
                room?.Trim(),
                member?.Trim(),
                result?.Trim(),
                kind,
                limit);
            return Results.Ok(await paifuFiles.ListAsync(auth.MemberNo, query));
        });

        app.MapPost("/api/player/paifu/{archiveId:long}/replay", async (
            long archiveId,
            HttpContext context,
            PaifuFileService paifuFiles,
            GameAuthTokenService gameAuth) =>
        {
            var auth = gameAuth.Validate(context.Request.Headers.Authorization.FirstOrDefault());
            if (auth is null) return Results.Unauthorized();
            if (archiveId <= 0) return Results.NotFound();
            var compressed = await paifuFiles.GetReplayObjectAsync(auth.MemberNo, (ulong)archiveId, context.RequestAborted);
            if (compressed is null) return Results.NotFound();
            context.Response.Headers.ContentEncoding = "br";
            return Results.File(compressed, "application/json");
        });

        app.MapGet("/api/shop/cash-products", async (
            HttpContext context,
            AdminRepository adminRepository,
            GameAuthTokenService gameAuth) =>
        {
            if (gameAuth.Validate(context.Request.Headers.Authorization.FirstOrDefault()) is null)
                return Results.Unauthorized();

            var products = await adminRepository.GetActiveWebCashProductsAsync();
            return Results.Ok(products.Select(product => new
            {
                productId = product.ProductId,
                displayName = product.DisplayName,
                cashAmount = product.CashAmount,
                priceJpy = product.PriceJpy,
            }));
        });

        app.MapGet("/api/shop/convenience-items", async (
            HttpContext context,
            ItemRepository itemRepository,
            GameAuthTokenService gameAuth) =>
        {
            if (gameAuth.Validate(context.Request.Headers.Authorization.FirstOrDefault()) is null)
                return Results.Unauthorized();
            return Results.Ok(await itemRepository.GetActiveBillingShopItemsAsync());
        });

        app.MapGet("/api/player/continue-room", async (
            HttpContext context,
            RoomRegistryService roomRegistry,
            ItemRepository itemRepository,
            PlayerSessionService sessions,
            GameAuthTokenService gameAuth) =>
        {
            var auth = gameAuth.Validate(context.Request.Headers.Authorization.FirstOrDefault());
            if (auth is null) return Results.Unauthorized();

            var room = await roomRegistry.GetContinueRoomAsync(auth.MemberNo);
            if (room is null) return Results.Ok(new { found = false });
            var customEquips = await itemRepository.GetEquippedCustomItemsAsync(auth.MemberNo);
            return Results.Ok(new
            {
                found = true,
                pix = sessions.GetPixByMemberNo(auth.MemberNo) ?? auth.Pix,
                roomId = room.RoomId,
                chanelId = room.ChanelId,
                channelId = room.ChanelId,
                title = room.Title,
                serverUrl = room.ServerUrl,
                roomOption = room.RoomOption,
                updatedAt = room.UpdatedAt,
                customEquips = customEquips
                    .Select(item => new { customType = item.Kind, customId = item.CustomId })
                    .ToArray(),
            });
        });
    }

    private static string EncodeCurrencyHistoryCursor(CurrencyHistoryCursor cursor)
        => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            $"{cursor.OccurredAt.Ticks}|{cursor.Source}|{cursor.Id}"));

    private static bool TryDecodeCurrencyHistoryCursor(string? value, out CurrencyHistoryCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        try
        {
            var values = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value)).Split('|');
            if (values.Length != 3 || !long.TryParse(values[0], out var ticks)
                || !ulong.TryParse(values[2], out var id)
                || values[1] is not ("money" or "cash")) return false;
            cursor = new CurrencyHistoryCursor(new DateTime(ticks), values[1], id);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
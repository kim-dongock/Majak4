﻿using Microsoft.AspNetCore.SignalR;
using MajakServer.Models.Protocol;
using MajakServer.Services;

namespace MajakServer.Commands.Channel;

/// <summary>
/// mjkc19e 称号取得通知。
/// 原典: commandGetTitle は HMajPlayer::CheckTitleClear() からの S→C Push 専用。
/// C→S ハンドラは存在しないため、受信しても何もしない。
/// </summary>
public class GetTitleCommand : ICommand
{
    public GetTitleCommand(TitleService titleService) { }

    public Task ExecuteAsync(CommandContext ctx) => Task.CompletedTask;
}
/// <summary>mjkc24e エモート使用</summary>


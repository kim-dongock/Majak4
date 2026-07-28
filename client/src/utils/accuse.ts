import * as SignalR from '../api/signalr'

const CMD_COMPLAINT = 'c63e'
const GAME_ID = 'MAJAK4'

export interface SendAccuseComplaintArgs {
  pix: string
  targetPix: string
  channelId?: string
  roomId?: number
  reasonIndex: number
  reason: string
  chatContent: string
}

export async function sendAccuseComplaint(args: SendAccuseComplaintArgs): Promise<void> {
  const reportingType = args.reasonIndex >= 5 ? 99 : args.reasonIndex

  await SignalR.send(CMD_COMPLAINT, {
    gameId: GAME_ID,
    k22e: GAME_ID,
    pix: args.pix,
    k3e: args.pix,
    targetPix: args.targetPix,
    opPix: args.targetPix,
    k4e: args.targetPix,
    channelId: args.channelId ?? '',
    k24e: args.channelId ?? '',
    roomId: args.roomId ?? 0,
    k42e: args.roomId ?? 0,
    reportingType,
    k81e: reportingType,
    string: args.reason,
    k41e: args.reason,
    description: args.chatContent,
    k63e: args.chatContent,
  })
}
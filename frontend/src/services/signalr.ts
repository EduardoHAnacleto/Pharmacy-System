import * as signalR from '@microsoft/signalr'

// Relative URL: served through the same nginx (or dev server) that serves the
// SPA, which is why nginx.conf forwards /promotionsHub with the Upgrade headers.
const connection = new signalR.HubConnectionBuilder()
  .withUrl('/promotionsHub', {
    withCredentials: true,
  })
  .withAutomaticReconnect()
  .build()

export async function startSignalR() {
  if (connection.state === signalR.HubConnectionState.Disconnected) {
    await connection.start()
  }
}

export function onPromotionsChanged(callback: () => void) {
  connection.on('PromotionsChanged', callback)
}

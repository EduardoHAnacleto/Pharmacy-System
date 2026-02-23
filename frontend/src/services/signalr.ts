import * as signalR from '@microsoft/signalr'

const connection = new signalR.HubConnectionBuilder()
  .withUrl('http://localhost:5000/promotionsHub', {
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

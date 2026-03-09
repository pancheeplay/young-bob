import { WebSocketServer } from 'ws';

const port = Number(process.env.PORT || 8787);
const wss = new WebSocketServer({ port });

let nextPlayerId = 1;
let nextRoomId = 1;

const clients = new Map();
const rooms = new Map();

function send(ws, type, payload) {
  if (ws.readyState !== ws.OPEN) {
    console.warn(`[relay] skip send ${type}, socket not open`);
    return;
  }

  console.log(`[relay] -> ${type}`, payload);
  ws.send(JSON.stringify({ type, payload }));
}

function roomListSnapshot() {
  return [...rooms.values()].map(room => ({
    roomId: room.roomId,
    roomName: room.roomName,
    roomType: room.roomType,
    playerCount: room.players.length,
    maxPlayerCount: room.maxPlayerCount
  }));
}

function buildRoomEvent(room, localPlayerId) {
  return {
    roomId: room.roomId,
    localPlayerId,
    hostPlayerId: room.hostPlayerId,
    players: room.players.map(player => ({
      playerId: player.playerId,
      displayName: player.displayName,
      isLocal: player.playerId === localPlayerId,
      isHost: player.playerId === room.hostPlayerId
    }))
  };
}

function broadcastRoom(room) {
  for (const player of room.players) {
    send(player.ws, 'room_joined', buildRoomEvent(room, player.playerId));
  }
}

function removePlayerFromRoom(player) {
  if (!player || !player.roomId) {
    return;
  }

  const room = rooms.get(player.roomId);
  if (!room) {
    player.roomId = null;
    return;
  }

  room.players = room.players.filter(entry => entry.playerId !== player.playerId);
  if (room.players.length === 0) {
    rooms.delete(room.roomId);
    player.roomId = null;
    return;
  }

  if (room.hostPlayerId === player.playerId) {
    room.hostPlayerId = room.players[0].playerId;
  }

  player.roomId = null;
  broadcastRoom(room);
}

function createRoomFor(player, roomName = 'Debug Room') {
  removePlayerFromRoom(player);

  const room = {
    roomId: String(nextRoomId++),
    roomName,
    roomType: 'young_bob_proto',
    maxPlayerCount: 2,
    hostPlayerId: player.playerId,
    players: [player]
  };

  player.roomId = room.roomId;
  rooms.set(room.roomId, room);
  console.log(`[relay] room created ${room.roomId} by ${player.playerId}`);
  broadcastRoom(room);
}

function matchRoomFor(player) {
  removePlayerFromRoom(player);

  const openRoom = [...rooms.values()].find(room =>
    room.roomType === 'young_bob_proto' &&
    room.players.length < room.maxPlayerCount);

  if (openRoom) {
    openRoom.players.push(player);
    player.roomId = openRoom.roomId;
    console.log(`[relay] matched ${player.playerId} into room ${openRoom.roomId}`);
    broadcastRoom(openRoom);
    return;
  }

  createRoomFor(player, 'Matched Room');
}

function joinRoom(player, roomId) {
  const room = rooms.get(roomId);
  if (!room) {
    send(player.ws, 'error', { message: `Room not found: ${roomId}` });
    return;
  }

  if (room.players.length >= room.maxPlayerCount) {
    send(player.ws, 'error', { message: `Room is full: ${roomId}` });
    return;
  }

  removePlayerFromRoom(player);
  room.players.push(player);
  player.roomId = room.roomId;
  console.log(`[relay] joined room ${room.roomId}: ${player.playerId}`);
  broadcastRoom(room);
}

function forwardRoomMessage(player, message) {
  if (!player.roomId) {
    console.error(`[relay] send_message rejected, player ${player.playerId} is not in a room`);
    send(player.ws, 'error', { message: 'Player is not in a room.' });
    return;
  }

  const room = rooms.get(player.roomId);
  if (!room) {
    console.error(`[relay] send_message rejected, room missing ${player.roomId}`);
    send(player.ws, 'error', { message: `Room not found: ${player.roomId}` });
    return;
  }

  console.log(`[relay] room_message from ${player.playerId} in room ${room.roomId}`, message);

  for (const peer of room.players) {
    if (peer.playerId === player.playerId) {
      continue;
    }

    send(peer.ws, 'room_message', message);
  }

  send(player.ws, 'send_ack', { messageId: message.messageId });
}

wss.on('connection', ws => {
  console.log('[relay] client connected');
  const player = {
    ws,
    playerId: null,
    displayName: 'Player',
    roomId: null
  };

  ws.on('message', raw => {
    let packet;
    try {
      packet = JSON.parse(raw.toString());
      console.log('[relay] <- packet', packet);
    } catch {
      console.error('[relay] invalid json packet');
      send(ws, 'error', { message: 'Invalid JSON packet.' });
      return;
    }

    const { type, payload } = packet;
    switch (type) {
      case 'connect':
        player.playerId = `debug_player_${nextPlayerId++}`;
        player.displayName = payload?.displayName || player.playerId;
        clients.set(ws, player);
        console.log(`[relay] player connected ${player.playerId} (${player.displayName})`);
        send(ws, 'connected', {
          playerId: player.playerId,
          displayName: player.displayName
        });
        break;

      case 'disconnect':
        removePlayerFromRoom(player);
        clients.delete(ws);
        send(ws, 'disconnected', {});
        break;

      case 'create_room':
        createRoomFor(player, payload?.roomName || 'Debug Room');
        break;

      case 'match_room':
        matchRoomFor(player);
        break;

      case 'get_room_list':
        send(ws, 'room_list', roomListSnapshot());
        break;

      case 'join_room':
        joinRoom(player, payload?.roomId);
        break;

      case 'leave_room':
        removePlayerFromRoom(player);
        send(ws, 'room_left', {});
        break;

      case 'send_message':
        forwardRoomMessage(player, payload);
        break;

      default:
        console.error(`[relay] unknown packet type ${type}`);
        send(ws, 'error', { message: `Unknown packet type: ${type}` });
        break;
    }
  });

  ws.on('close', () => {
    console.log(`[relay] client closed ${player.playerId ?? 'unknown'}`);
    removePlayerFromRoom(player);
    clients.delete(ws);
  });

  ws.on('error', error => {
    console.error('[relay] websocket error', error);
  });
});

console.log(`Young Bob debug relay listening on ws://0.0.0.0:${port}`);

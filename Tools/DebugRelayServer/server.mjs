import { WebSocketServer } from 'ws';

const port = Number(process.env.PORT || 8787);
const wss = new WebSocketServer({ port });
const verboseLogging = process.env.DEBUG_RELAY_VERBOSE === '1';

let nextPlayerId = 1;
let nextRoomId = 1;

const clients = new Map();
const rooms = new Map();

function timestamp() {
  return new Date().toISOString().slice(11, 19);
}

function log(level, scope, message, details = null) {
  const line = `[${timestamp()}] [${level}] [${scope}] ${message}`;
  if (details == null) {
    console.log(line);
    return;
  }

  console.log(`${line}\n${JSON.stringify(details, null, 2)}`);
}

function info(scope, message, details = null) {
  log('INFO', scope, message, details);
}

function warn(scope, message, details = null) {
  log('WARN', scope, message, details);
}

function error(scope, message, details = null) {
  log('ERROR', scope, message, details);
}

function verbose(scope, message, details = null) {
  if (!verboseLogging) {
    return;
  }

  log('DEBUG', scope, message, details);
}

function summarizeMessage(message) {
  if (!message) {
    return { type: 'unknown' };
  }

  return {
    messageId: message.messageId ?? '-',
    type: message.type ?? 'unknown',
    senderPlayerId: message.senderPlayerId ?? '-',
    roomId: message.roomId ?? '-',
    seq: message.seq ?? '-',
    payloadBytes: typeof message.payloadJson === 'string' ? message.payloadJson.length : 0
  };
}

function send(ws, type, payload) {
  if (ws.readyState !== ws.OPEN) {
    warn('send', `skip ${type}, socket not open`);
    return;
  }

  verbose('send', `-> ${type}`, payload);
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
  info('room', `created room=${room.roomId} host=${player.playerId} name="${room.roomName}"`);
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
    info('match', `player=${player.playerId} joined existing room=${openRoom.roomId} players=${openRoom.players.length}/${openRoom.maxPlayerCount}`);
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
  info('room', `player=${player.playerId} joined room=${room.roomId} players=${room.players.length}/${room.maxPlayerCount}`);
  broadcastRoom(room);
}

function forwardRoomMessage(player, message) {
  if (!player.roomId) {
    error('message', `rejected send_message, player=${player.playerId} is not in a room`);
    send(player.ws, 'error', { message: 'Player is not in a room.' });
    return;
  }

  const room = rooms.get(player.roomId);
  if (!room) {
    error('message', `rejected send_message, room missing room=${player.roomId}`);
    send(player.ws, 'error', { message: `Room not found: ${player.roomId}` });
    return;
  }

  info('message', `forward room=${room.roomId} from=${player.playerId} to=${Math.max(0, room.players.length - 1)} peers type=${message?.type ?? 'unknown'} seq=${message?.seq ?? '-'} bytes=${typeof message?.payloadJson === 'string' ? message.payloadJson.length : 0}`);
  verbose('message', 'payload', summarizeMessage(message));

  for (const peer of room.players) {
    if (peer.playerId === player.playerId) {
      continue;
    }

    send(peer.ws, 'room_message', message);
  }

  send(player.ws, 'send_ack', { messageId: message.messageId });
}

wss.on('connection', ws => {
  info('socket', 'client connected');
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
      verbose('recv', '<- packet', packet);
    } catch {
      error('recv', 'invalid json packet');
      send(ws, 'error', { message: 'Invalid JSON packet.' });
      return;
    }

    const { type, payload } = packet;
    switch (type) {
      case 'connect':
        player.playerId = `debug_player_${nextPlayerId++}`;
        player.displayName = payload?.displayName || player.playerId;
        clients.set(ws, player);
        info('player', `connected id=${player.playerId} name="${player.displayName}" activeClients=${clients.size}`);
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
        error('recv', `unknown packet type=${type}`);
        send(ws, 'error', { message: `Unknown packet type: ${type}` });
        break;
    }
  });

  ws.on('close', () => {
    info('socket', `client closed player=${player.playerId ?? 'unknown'}`);
    removePlayerFromRoom(player);
    clients.delete(ws);
  });

  ws.on('error', error => {
    error('socket', 'websocket error', {
      message: error?.message ?? String(error)
    });
  });
});

info('server', `listening on ws://0.0.0.0:${port} verbose=${verboseLogging ? 'on' : 'off'}`);

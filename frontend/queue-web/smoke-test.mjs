import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'

const API = 'http://localhost:5244'

async function post(path, body, token) {
  const res = await fetch(`${API}${path}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
    },
    body: JSON.stringify(body ?? {}),
  })
  const json = await res.json().catch(() => ({}))
  if (!res.ok || json.success === false) {
    throw new Error(`${path} -> ${res.status} ${JSON.stringify(json)}`)
  }
  return json.data
}

async function get(path, token) {
  const res = await fetch(`${API}${path}`, {
    headers: token ? { Authorization: `Bearer ${token}` } : {},
  })
  const json = await res.json().catch(() => ({}))
  if (!res.ok || json.success === false) {
    throw new Error(`${path} -> ${res.status} ${JSON.stringify(json)}`)
  }
  return json.data
}

const received = []

const connection = new HubConnectionBuilder()
  .withUrl(`${API}/hubs/queue`, { accessTokenFactory: () => token ?? '' })
  .configureLogging(LogLevel.Error)
  .build()

for (const name of [
  'QueueCreated', 'QueueCalled', 'QueueRecalled', 'QueueStarted',
  'QueueCompleted', 'QueueNoShow', 'QueueCancelled', 'QueueTransferred',
  'QueueUpdated', 'DisplayUpdated',
]) {
  connection.on(name, (payload) => {
    // 注意：不可用簡式箭頭 (x => arr.push(x))，push 會回傳數字，
    // 導致 SignalR client 記錄 "Result given for X but server is not expecting a result"
    received.push({ name, payload })
  })
}

let token = null
try {
  await connection.start()
  console.log('hub connected:', connection.state)

  const login = await post('/api/auth/login', { userName: 'admin', password: 'a12345678' })
  token = login.accessToken
  console.log('admin logged in')

  const services = await get('/api/queue/services', token)
  const service = services.find((s) => s.code === 'VIP') ?? services[0]

  const ticket = await post('/api/queue/tickets', { serviceId: service.id }, token)
  console.log('ticket created:', ticket.ticketNo)

  const counters = await get('/api/queue/counters')
  const counter = counters.find((c) => c.serviceId === service.id && c.status === 'Idle')
    ?? counters.find((c) => c.serviceId === service.id)
    ?? counters[0]

  // 清掉先前測試殘留的「服務中」票，避免 COUNTER_BUSY
  if (counter.status === 'Busy') {
    console.log('resetting busy counter', counter.code)
    await post(`/api/queue/counters/${counter.id}/no-show`, { remark: 'reset by smoke' }, token)
  }

  const called = await post(`/api/queue/counters/${counter.id}/call-next`, {}, token)
  console.log('called:', called.ticketNo, '| counter:', counter.code)

  await new Promise((r) => setTimeout(r, 1500))

  console.log('events received:', received.map((e) => e.name).join(', ') || '(none)')
  const display = received.find((e) => e.name === 'DisplayUpdated')
  const created = received.find((e) => e.name === 'QueueCreated')
  const calledEvt = received.find((e) => e.name === 'QueueCalled')

  console.log('  QueueCreated payload keys =', Object.keys(created?.payload ?? {}).join(','))
  console.log('  QueueCreated raw =', JSON.stringify(created?.payload))
  console.log('  DisplayUpdated counters =', display?.payload?.counters?.length)

  if (called?.ticketNo) {
    const started = await post(`/api/queue/counters/${counter.id}/start`, {}, token)
    console.log('started:', started.ticketNo, started.status)
    const done = await post(`/api/queue/counters/${counter.id}/complete`, { remark: 'smoke' }, token)
    console.log('completed:', done.ticketNo, done.status)
  }

  await new Promise((r) => setTimeout(r, 1000))
  console.log('final events:', received.map((e) => e.name).join(', '))

  const ok = received.some((e) => e.name === 'QueueCreated') &&
    received.some((e) => e.name === 'QueueCalled') &&
    received.some((e) => e.name === 'DisplayUpdated') &&
    received.some((e) => e.name === 'QueueCompleted')
  console.log(ok ? '\nRESULT: PASS' : '\nRESULT: FAIL')
  process.exit(ok ? 0 : 1)
} catch (err) {
  console.error('ERROR:', err.message)
  process.exit(1)
} finally {
  await connection.stop().catch(() => {})
}

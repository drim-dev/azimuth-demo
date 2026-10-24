interface ArchitectureStep {
  title: string
  detail: string
  activeNodes: string[]
  activeConnections: string[]
  dangerNodes?: string[]
}

interface RefundArchitectureSvgProps {
  svgId: string
  step: ArchitectureStep
  stepIndex: number
  finalStepIndex: number
  ledgerEntry: string
}

interface ServiceNode {
  id: string
  x: number
  width: number
  label: string[]
  detail: string[]
  kind?: 'queue'
}

const serviceNodes: ServiceNode[] = [
  { id: 'people', x: 10, width: 170, label: ['Пассажир', 'и поддержка'], detail: ['браузер'] },
  { id: 'web', x: 210, width: 160, label: ['Веб-приложение'], detail: ['Next.js + BFF'] },
  {
    id: 'api',
    x: 405,
    width: 170,
    label: ['Refund API'],
    detail: ['возвраты', 'исходящие события'],
  },
  {
    id: 'broker',
    x: 615,
    width: 150,
    label: ['RabbitMQ'],
    detail: ['команды', 'результаты'],
    kind: 'queue',
  },
  { id: 'worker', x: 805, width: 160, label: ['Обработчик'], detail: ['фоновый процесс'] },
  {
    id: 'provider',
    x: 1005,
    width: 185,
    label: ['Платёжный', 'провайдер'],
    detail: ['внешняя система'],
  },
]

const connections = [
  { id: 'people-web', x1: 180, x2: 210 },
  { id: 'web-api', x1: 370, x2: 405 },
  { id: 'api-broker', x1: 575, x2: 615, bidirectional: true },
  { id: 'broker-worker', x1: 765, x2: 805, bidirectional: true },
  { id: 'worker-provider', x1: 965, x2: 1005 },
]

function ArrowMarker({ id, color }: { id: string; color: string }) {
  return (
    <marker
      id={id}
      markerWidth="10"
      markerHeight="10"
      refX="8"
      refY="5"
      orient="auto-start-reverse"
    >
      <path d="M 1 1 L 9 5 L 1 9" fill="none" stroke={color} strokeWidth="1.8" />
    </marker>
  )
}

function QueuePattern({ id, active }: { id: string; active: boolean }) {
  return (
    <pattern
      id={id}
      width="10"
      height="10"
      patternUnits="userSpaceOnUse"
      patternTransform="rotate(45)"
    >
      <rect width="10" height="10" fill={active ? 'var(--accent-fill)' : 'var(--surface)'} />
      <line
        x1="0"
        y1="0"
        x2="0"
        y2="10"
        stroke={active ? 'var(--accent)' : 'var(--line)'}
        strokeWidth="4"
        opacity={active ? 0.4 : 0.35}
      />
    </pattern>
  )
}

function ServiceNodeShape({
  node,
  active,
  danger,
  svgId,
}: {
  node: ServiceNode
  active: boolean
  danger: boolean
  svgId: string
}) {
  const fill = danger
    ? 'var(--danger-fill)'
    : node.kind === 'queue'
      ? `url(#${svgId}-queue${active ? '-active' : ''})`
      : active
        ? 'var(--accent-fill)'
        : 'var(--surface)'
  const stroke = danger ? 'var(--danger)' : active ? 'var(--accent)' : 'var(--line)'
  const labelColor = danger ? 'var(--danger)' : active ? 'var(--accent)' : 'var(--ink)'

  return (
    <g>
      <rect
        className="node-shape"
        x={node.x}
        y="55"
        width={node.width}
        height="100"
        rx={node.kind === 'queue' ? 2 : 12}
        fill={fill}
        stroke={stroke}
        strokeWidth={active || danger ? 3 : 2}
      />
      {node.kind === 'queue' && (
        <>
          <line x1={node.x + 12} y1="55" x2={node.x + 12} y2="155" stroke={stroke} strokeWidth="2" />
          <line
            x1={node.x + node.width - 12}
            y1="55"
            x2={node.x + node.width - 12}
            y2="155"
            stroke={stroke}
            strokeWidth="2"
          />
        </>
      )}
      <text
        x={node.x + node.width / 2}
        y={node.label.length === 1 ? 92 : 82}
        textAnchor="middle"
        fill={labelColor}
        fontSize="20"
        fontWeight="700"
      >
        {node.label.map((line, index) => (
          <tspan key={line} x={node.x + node.width / 2} dy={index === 0 ? 0 : 23}>
            {line}
          </tspan>
        ))}
      </text>
      <text
        x={node.x + node.width / 2}
        y={node.detail.length === 1 ? 137 : 126}
        textAnchor="middle"
        fill="var(--muted)"
        fontSize="15"
      >
        {node.detail.map((line, index) => (
          <tspan key={line} x={node.x + node.width / 2} dy={index === 0 ? 0 : 18}>
            {line}
          </tspan>
        ))}
      </text>
    </g>
  )
}

type DatabaseState = 'idle' | 'active' | 'success' | 'danger'

function DatabaseShape({
  x,
  width,
  title,
  detail,
  state,
}: {
  x: number
  width: number
  title: string
  detail: string
  state: DatabaseState
}) {
  const center = x + width / 2
  const fill =
    state === 'danger'
      ? 'var(--danger-fill)'
      : state === 'success'
        ? 'var(--success-fill)'
        : state === 'active'
          ? 'var(--accent-fill)'
          : 'var(--surface)'
  const stroke =
    state === 'danger'
      ? 'var(--danger)'
      : state === 'success'
        ? 'var(--success)'
        : state === 'active'
          ? 'var(--accent)'
          : 'var(--line)'

  return (
    <g>
      <rect x={x} y="220" width={width} height="64" fill={fill} stroke={stroke} strokeWidth="2" />
      <ellipse cx={center} cy="220" rx={width / 2} ry="13" fill={fill} stroke={stroke} strokeWidth="2" />
      <path
        d={`M${x} 284 A${width / 2} 13 0 0 0 ${x + width} 284`}
        fill="none"
        stroke={stroke}
        strokeWidth="2"
      />
      <text x={center} y="244" textAnchor="middle" fill="var(--ink)" fontSize="17" fontWeight="700">
        {title}
      </text>
      <text
        x={center}
        y="267"
        textAnchor="middle"
        fill={state === 'danger' ? 'var(--danger)' : 'var(--muted)'}
        fontSize="14"
        fontWeight={state === 'danger' ? '700' : '400'}
      >
        {detail}
      </text>
    </g>
  )
}

export function RefundArchitectureSvg({
  svgId,
  step,
  stepIndex,
  finalStepIndex,
  ledgerEntry,
}: RefundArchitectureSvgProps) {
  return (
    <svg
      viewBox="0 0 1200 330"
      className="refund-architecture h-auto w-full"
      role="img"
      aria-label={`Схема системы возвратов. ${step.title}. ${step.detail}`}
    >
      <title>{`Система возвратов: ${step.title}`}</title>
      <style>{`
        .refund-architecture {
          --surface: #ffffff;
          --ink: #1c1917;
          --muted: #57534e;
          --line: #a8a29e;
          --accent: #d55020;
          --accent-fill: #fff5f2;
          --danger: #dc2626;
          --danger-fill: #fef2f2;
          --success: #059669;
          --success-fill: #ecfdf5;
        }
        .dark .refund-architecture,
        :root.dark .refund-architecture {
          --surface: #1f2937;
          --ink: #f5f5f4;
          --muted: #a8a29e;
          --line: #6b7280;
          --accent: #ff8c66;
          --accent-fill: #3f1d14;
          --danger: #f87171;
          --danger-fill: #3f1118;
          --success: #34d399;
          --success-fill: #052e2b;
        }
        .refund-architecture .node-shape,
        .refund-architecture .connection {
          transition: fill 180ms ease, stroke 180ms ease;
        }
        @media (prefers-reduced-motion: reduce) {
          .refund-architecture .node-shape,
          .refund-architecture .connection { transition: none; }
        }
      `}</style>
      <defs>
        <ArrowMarker id={`${svgId}-arrow`} color="var(--line)" />
        <ArrowMarker id={`${svgId}-arrow-active`} color="var(--accent)" />
        <QueuePattern id={`${svgId}-queue`} active={false} />
        <QueuePattern id={`${svgId}-queue-active`} active />
      </defs>

      <g aria-hidden="true">
        {connections.map((connection) => {
          const active = step.activeConnections.includes(connection.id)
          const markerId = active ? `${svgId}-arrow-active` : `${svgId}-arrow`

          return (
            <line
              key={connection.id}
              className="connection"
              x1={connection.x1 + 6}
              y1="105"
              x2={connection.x2 - 6}
              y2="105"
              stroke={active ? 'var(--accent)' : 'var(--line)'}
              strokeWidth={active ? 3 : 2}
              markerStart={connection.bidirectional ? `url(#${markerId})` : undefined}
              markerEnd={`url(#${markerId})`}
            />
          )
        })}

        <line x1="490" y1="155" x2="490" y2="207" stroke="var(--line)" strokeWidth="2" />
        <line x1="1098" y1="155" x2="1098" y2="207" stroke="var(--line)" strokeWidth="2" />

        {serviceNodes.map((node) => (
          <ServiceNodeShape
            key={node.id}
            node={node}
            active={step.activeNodes.includes(node.id)}
            danger={step.dangerNodes?.includes(node.id) ?? false}
            svgId={svgId}
          />
        ))}

        <DatabaseShape
          x={425}
          width={130}
          title="PostgreSQL API"
          detail={stepIndex >= 1 ? 'RefundId + событие' : 'состояние возвратов'}
          state={stepIndex >= 1 ? 'active' : 'idle'}
        />
        <DatabaseShape
          x={1028}
          width={140}
          title="Финансовый реестр"
          detail={ledgerEntry}
          state={stepIndex === finalStepIndex ? 'danger' : stepIndex >= 3 ? 'success' : 'idle'}
        />
      </g>
    </svg>
  )
}

import { describe, expect, it } from 'vitest'

import { authTypeLabel, formatEpochMs, statusMeta, transportLabel } from './constants'

describe('mcp constants', () => {
  it('statusMeta 映射已知状态，未知回退 info', () => {
    expect(statusMeta('connected')).toEqual({ label: '已连接', type: 'success' })
    expect(statusMeta('error')).toEqual({ label: '连接异常', type: 'danger' })
    expect(statusMeta('unknown')).toEqual({ label: '未探测', type: 'info' })
    expect(statusMeta('weird')).toEqual({ label: 'weird', type: 'info' })
    expect(statusMeta('')).toEqual({ label: '未知', type: 'info' })
  })

  it('authTypeLabel 映射已知值，未知原样返回', () => {
    expect(authTypeLabel('bearer')).toBe('Bearer')
    expect(authTypeLabel('none')).toBe('无鉴权')
    expect(authTypeLabel('custom')).toBe('custom')
  })

  it('transportLabel 美化 streamable_http', () => {
    expect(transportLabel('streamable_http')).toBe('Streamable HTTP')
    expect(transportLabel('other')).toBe('other')
  })

  it('formatEpochMs：0 视为「从未」', () => {
    expect(formatEpochMs(0)).toBe('—')
    expect(formatEpochMs(1_700_000_000_000)).not.toBe('—')
  })
})

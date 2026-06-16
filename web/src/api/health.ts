import axios from 'axios'

import type { Result } from './types'

/** 单个健康检查项 */
export interface HealthCheckItem {
  name: string
  status: string
  description: string
}

/** 健康检查返回（对应后端 /api/v1/health 的 data） */
export interface HealthDto {
  status: string
  totalDurationMs: number
  checks: HealthCheckItem[]
}

const base = import.meta.env.VITE_API_BASE_URL

/**
 * 探活：独立请求，不走全局拦截器（避免失败时弹全局错误提示）。
 * 连通性测试 10s 超时，对齐后端约定。失败时抛错，由调用方处理。
 */
export async function getHealth(): Promise<HealthDto> {
  const { data } = await axios.get<Result<HealthDto>>(`${base}/health`, { timeout: 10_000 })
  if (data.code !== 200) {
    throw new Error(data.message || 'unhealthy')
  }
  return data.data
}

import axios, { AxiosError, type AxiosInstance, type AxiosResponse } from 'axios'
import { ElMessage } from 'element-plus'

import { SUCCESS_CODE, resolveErrorMessage } from '@/constants/error-code'
import type { Page, PageResult, Result } from './types'

/**
 * 业务错误：后端返回了响应但 code !== 200。
 * 与网络/HTTP 层错误区分，便于上层按 code 分支处理。
 */
export class ApiError extends Error {
  readonly code: number

  constructor(code: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.code = code
  }
}

const http: AxiosInstance = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL,
  // 同步/CRUD 调用 60s，与后端同步调用超时对齐；SSE 流式不走此实例
  timeout: 60_000,
})

http.interceptors.response.use(
  (response: AxiosResponse<Result<unknown>>) => response,
  (error: AxiosError) => {
    // 网络/超时/非 2xx：统一提示后向上抛出
    const message = error.code === 'ECONNABORTED' ? '请求超时' : '网络异常，请稍后重试'
    ElMessage.error(message)
    return Promise.reject(error)
  },
)

/** 拆 Result<T>：成功返回 data，失败抛 ApiError 并弹出提示 */
async function unwrap<T>(promise: Promise<AxiosResponse<Result<T>>>): Promise<T> {
  const { data: body } = await promise
  if (body.code !== SUCCESS_CODE) {
    const msg = resolveErrorMessage(body.code, body.message)
    ElMessage.error(msg)
    throw new ApiError(body.code, msg)
  }
  return body.data
}

/** 拆 PageResult<T>：返回归一化的 Page<T> */
async function unwrapPage<T>(promise: Promise<AxiosResponse<PageResult<T>>>): Promise<Page<T>> {
  const { data: body } = await promise
  if (body.code !== SUCCESS_CODE) {
    const msg = resolveErrorMessage(body.code, body.message)
    ElMessage.error(msg)
    throw new ApiError(body.code, msg)
  }
  return { items: body.data, total: body.total, page: body.page, size: body.size }
}

export const api = {
  get: <T>(url: string, params?: object) => unwrap<T>(http.get(url, { params })),
  getPage: <T>(url: string, params?: object) => unwrapPage<T>(http.get(url, { params })),
  post: <T>(url: string, data?: object) => unwrap<T>(http.post(url, data)),
  put: <T>(url: string, data?: object) => unwrap<T>(http.put(url, data)),
  delete: <T>(url: string) => unwrap<T>(http.delete(url)),
}

export default http

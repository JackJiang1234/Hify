/**
 * 与后端 Hify.Shared.Results 对齐的通用响应契约。
 * 后端所有接口统一返回 Result<T>，分页返回 PageResult<T>。
 */

/** 统一响应包：code===200 表示成功，否则 code 为四位业务错误码 */
export interface Result<T> {
  code: number
  message: string
  data: T
}

/** 分页响应：data 即当前页列表，额外带 total/page/size */
export interface PageResult<T> {
  code: number
  message: string
  data: T[]
  total: number
  page: number
  size: number
}

/** 分页请求参数：page 从 1 开始，size 默认 20、上限 100 */
export interface PageQuery {
  page?: number
  size?: number
}

/** 拦截器拆包后向上层暴露的分页结果（已剥离 code/message） */
export interface Page<T> {
  items: T[]
  total: number
  page: number
  size: number
}

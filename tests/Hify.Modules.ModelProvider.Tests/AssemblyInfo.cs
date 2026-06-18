// 本测试程序集内多处集成测试共享同一个 PostgreSQL（HIFY_TEST_DB）。
// 关闭并行，避免并发创建数据导致 OFFSET 分页等断言出现非确定性（与生产无关，纯测试隔离）。
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

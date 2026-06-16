<script setup lang="ts">
// 示例视图：展示 api/composable 接线方式，业务实现后续补全
import { useProviderList } from '../composables/useProviderList'

const { rows, total, page, size, loading, load } = useProviderList()

void load()
</script>

<template>
  <el-table v-loading="loading" :data="rows" stripe>
    <el-table-column prop="name" label="名称" />
    <el-table-column prop="type" label="类型" />
    <el-table-column prop="baseUrl" label="接入地址" />
  </el-table>
  <el-pagination
    v-model:current-page="page"
    v-model:page-size="size"
    :total="total"
    layout="total, prev, pager, next"
    @current-change="load"
  />
</template>

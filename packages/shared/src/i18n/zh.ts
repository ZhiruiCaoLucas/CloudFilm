import type { Messages } from './en';

/**
 * The Simplified Chinese catalogue.
 *
 * Typed as {@link Messages}, so it cannot omit a key, add one, or nest it
 * differently without failing the build.
 *
 * Clinical values stay untranslated by construction: nothing in here maps a
 * patient name, an identifier, a DICOM description, a modality code or a date.
 * `中文` is left as-is in the language switcher, because a language's own name
 * is what a reader looking for it expects to see.
 */
export const zh: Messages = {
  common: {
    appName: 'CloudFilm',
    tagline: '云影像平台',
    description: '通过浏览器安全访问医院影像检查。',
    skipToContent: '跳至主要内容',
    retry: '重试',
    version: '版本',
    build: '构建',
    built: '构建时间',
    reference: '参考编号 {digest} —— 报告问题时请提供此编号。',
  },

  locale: {
    label: '语言',
    en: 'English',
    zh: '中文',
  },

  nav: {
    signIn: '登录',
    signOut: '退出登录',
    environmentDevelopment: '开发环境',
    environmentProduction: '生产环境',
  },

  home: {
    title: '云影像平台',
    subtitle: '安全访问医学影像检查。',
    pageTitle: '患者检索 · CloudFilm',
    searchLabel: '患者标识',
    searchHint: '输入患者的病历号以查看其影像检查。',
    searchPlaceholder: '例如 10001',
    searchSubmit: '检索',
    searchSubmitting: '检索中…',
    errorEmpty: '请输入要检索的患者标识。',
    errorFormat: '患者标识只能包含字母、数字和连字符。',
    recentTitle: '最近检索',
    recentEmptyTitle: '暂无最近检索',
    recentEmptyBody: '在登录与访问审计就绪后，您打开过的患者将显示在此处。',
  },

  patient: {
    breadcrumb: '← 患者检索',
    name: '姓名',
    medicalRecordNumber: '病历号',
    gender: '性别',
    birthDate: '出生日期',
    breadcrumbLabel: '面包屑导航',
    studiesHeading: '检查',
    studyCountOne: '{count} 项检查',
    studyCountOther: '{count} 项检查',
    loading: '正在加载患者记录',
    notFoundTitle: '未找到匹配的患者',
    notFoundBody: '未找到使用该病历号的患者。请核对号码后重新检索。',
    notFoundKnown: '已有的病历号：',
    backToSearch: '返回患者检索',
    errorTitle: '无法显示该患者记录',
    errorBody: '数据未被更改。请重试，或返回检索后再试。',
  },

  gender: {
    Male: '男',
    Female: '女',
    Other: '其他',
    Unknown: '未知',
  },

  study: {
    tableCaption: '该患者的影像检查',
    modality: '检查设备',
    description: '检查描述',
    date: '检查日期',
    series: '序列数',
    status: '状态',
    actions: '操作',
    open: '打开检查',
    accession: '检查号',
    images: '图像数',
    studyUid: '检查实例 UID',
    statusCompleted: '已完成',
    statusInProgress: '进行中',
    statusCancelled: '已取消',
  },

  series: {
    numbered: '序列 {number}',
    unnumbered: '序列',
    modality: '检查设备',
    description: '序列描述',
    images: '图像数',
    institution: '检查机构',
  },

  workspace: {
    patient: '患者',
    mrn: '病历号',
    study: '检查',
    date: '日期',
    born: '出生',
    gender: '性别',
    studiesCount: '检查（{count}）',
    navigatorLabel: '检查导航',
    studiesLabel: '检查列表',
    metadata: '元数据',
    metadataLabel: '检查元数据',
    hide: '隐藏',
    info: '信息',
    statusLabel: '工作区状态',
    shortcutHint: '上一项 / 下一项检查',
    loading: '正在加载检查…',
    notFoundTitle: '未找到该检查',
    notFoundBody:
      '没有与该检查实例 UID 对应的检查。它可能属于当前部署未包含的患者，也可能是链接不完整。',
    errorTitle: '无法打开该检查',
    errorBody: '工作区加载失败。影像数据未被更改。',
    patientSearch: '患者检索',
    pageTitle: '影像工作区 · CloudFilm',
  },

  viewer: {
    label: '影像阅片区',
    title: 'OHIF 阅片器 — {description}',
    noDataTitle: '该检查没有影像数据',
    noDataBody: '这是一条占位检查，其标识为合成生成，没有任何归档持有对应的像素数据。',
    noDataHint: '使用 {command} 将检查导入本地 Orthanc，然后重启门户并检索它输出的病历号。',
  },

  auth: {
    pageTitle: '登录 · CloudFilm',
    title: '登录 CloudFilm',
    body: '账号由管理员发放，CloudFilm 不提供自助注册。',
    email: '电子邮箱',
    password: '密码',
    submit: '登录',
    submitting: '正在登录…',
    invalidCredentials: '凭据无效',
    missingFields: '请输入电子邮箱和密码。',
    unreachable: '无法连接 CloudFilm，请重试。',
  },
};

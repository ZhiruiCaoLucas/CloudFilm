/**
 * The English message catalogue, and the shape every other locale must match.
 *
 * English is the source of truth: `Messages` is derived from this object, so a
 * translation that misses a key, adds one, or nests it differently fails to
 * compile rather than falling back to a blank string at a clinician's
 * workstation.
 *
 * Clinical data is never in here. Patient names, medical record numbers,
 * accession numbers, UIDs, DICOM descriptions, modality codes, institution
 * names and dates arrive from the archive and are rendered verbatim - a
 * translated identifier is a wrong identifier. Only the labels around them are
 * translated.
 */
export const en = {
  common: {
    appName: 'CloudFilm',
    tagline: 'Cloud Imaging Platform',
    description: 'Secure browser access to hospital imaging studies.',
    skipToContent: 'Skip to content',
    retry: 'Retry',
    version: 'Version',
    build: 'Build',
    built: 'Built',
    reference: 'Reference {digest} — quote this when reporting the problem.',
  },

  locale: {
    label: 'Language',
    en: 'English',
    zh: '中文',
  },

  nav: {
    signIn: 'Sign in',
    signOut: 'Sign out',
    environmentDevelopment: 'Development',
    environmentProduction: 'Production',
  },

  home: {
    title: 'Cloud Imaging Platform',
    subtitle: 'Secure access to medical imaging studies.',
    pageTitle: 'Patient search · CloudFilm',
    searchLabel: 'Patient ID',
    searchHint: 'Enter the patient\u2019s Medical Record Number to open their imaging studies.',
    searchPlaceholder: 'e.g. 10001',
    searchSubmit: 'Search',
    searchSubmitting: 'Searching…',
    errorEmpty: 'Enter a patient ID to search.',
    errorFormat: 'A patient ID may contain letters, numbers and hyphens only.',
    recentTitle: 'Recent searches',
    recentEmptyTitle: 'No recent searches',
    recentEmptyBody: 'Patients you open will be listed here.',
    advancedSearch: 'Search by name, accession number or date',
  },

  search: {
    pageTitle: 'Study search · CloudFilm',
    title: 'Search studies',
    subtitle: 'Find studies by patient name, accession number or date.',
    breadcrumb: '← Patient search',
    patientName: 'Patient name',
    patientNameHint: 'Matches the start of the name as the hospital recorded it.',
    accessionNumber: 'Accession number',
    dateFrom: 'Study date from',
    dateTo: 'Study date to',
    submit: 'Search',
    submitting: 'Searching…',
    clear: 'Clear',
    errorEmpty: 'Enter at least one search criterion.',
    errorDateOrder: 'The start date must not be later than the end date.',
    promptTitle: 'No search yet',
    promptBody: 'Fill in at least one field above to search for studies.',
    resultsHeading: 'Results',
    resultCountOne: '{count} study on this page',
    resultCountOther: '{count} studies on this page',
    moreAvailable: 'More studies match. Narrow the search, or continue to the next page.',
    noResultsTitle: 'No matching studies',
    noResultsBody: 'Nothing matched those criteria. Check the spelling, or widen the date range.',
    invalidTitle: 'That search could not be run',
    invalidBody: 'Check the dates and try again. Enter at least one criterion.',
    patient: 'Patient',
    previous: 'Previous',
    next: 'Next',
    pagePosition: 'Page {page}',
    tableCaption: 'Studies matching the search criteria',
  },

  patient: {
    breadcrumb: '← Patient search',
    name: 'Name',
    medicalRecordNumber: 'Medical record number',
    gender: 'Gender',
    birthDate: 'Birth date',
    breadcrumbLabel: 'Breadcrumb',
    studiesHeading: 'Studies',
    studyCountOne: '{count} study',
    studyCountOther: '{count} studies',
    loading: 'Loading patient record',
    notFoundTitle: 'No matching patient',
    notFoundBody:
      'No patient with that Medical Record Number was found. Check the number and search again.',
    notFoundKnown: 'Existing medical record numbers:',
    backToSearch: 'Back to patient search',
    errorTitle: 'This patient record could not be displayed',
    errorBody: 'Nothing has been changed. Retry, or return to search and try again.',
  },

  gender: {
    Male: 'Male',
    Female: 'Female',
    Other: 'Other',
    Unknown: 'Unknown',
  },

  study: {
    tableCaption: 'Imaging studies for this patient',
    modality: 'Modality',
    description: 'Description',
    date: 'Study date',
    series: 'Series',
    status: 'Status',
    actions: 'Actions',
    open: 'Open Study',
    accession: 'Accession',
    images: 'Images',
    studyUid: 'Study UID',
    statusCompleted: 'Completed',
    statusInProgress: 'In progress',
    statusCancelled: 'Cancelled',
  },

  series: {
    numbered: 'Series {number}',
    unnumbered: 'Series',
    modality: 'Modality',
    description: 'Description',
    images: 'Images',
    institution: 'Institution',
  },

  workspace: {
    patient: 'Patient',
    mrn: 'MRN',
    study: 'Study',
    date: 'Date',
    born: 'Born',
    gender: 'Gender',
    studiesCount: 'Studies ({count})',
    navigatorLabel: 'Study navigator',
    studiesLabel: 'Studies',
    metadata: 'Metadata',
    metadataLabel: 'Study metadata',
    hide: 'Hide',
    info: 'Info',
    statusLabel: 'Workspace status',
    read: 'Read',
    unread: 'Unread',
    markRead: 'Mark this study as read',
    markUnread: 'Mark this study as unread',
    readStateSaving: 'Saving…',
    shortcutHint: 'previous / next study',
    loading: 'Loading study…',
    notFoundTitle: 'Study not found',
    notFoundBody:
      'No study with that Study Instance UID is available. It may belong to a patient this build does not carry, or the link may be incomplete.',
    errorTitle: 'This study could not be opened',
    errorBody: 'The workspace failed to load. No imaging data has been altered.',
    patientSearch: 'Patient search',
    pageTitle: 'Imaging workspace · CloudFilm',
  },

  viewer: {
    label: 'Image viewer',
    title: 'OHIF Viewer — {description}',
    noDataTitle: 'No imaging data for this study',
    noDataBody:
      'This is a placeholder study. Its identifier is synthetic, so no archive holds pixel data for it.',
    noDataHint:
      'Import a study into the local Orthanc with {command}, then restart the portal and search the record number it prints.',
  },

  noRole: {
    pageTitle: 'Access not enabled · CloudFilm',
    title: 'No application role',
    body: 'Your account is authenticated, but it has not been assigned an application role. Please contact an administrator to request access.',
    detail:
      'Roles are assigned by an administrator. CloudFilm has no self-service way to request or grant one.',
    home: 'Return to the start page',
  },

  auth: {
    pageTitle: 'Sign in · CloudFilm',
    title: 'Sign in to CloudFilm',
    body: 'Accounts are issued by your administrator. CloudFilm has no self-service registration.',
    email: 'E-mail address',
    password: 'Password',
    submit: 'Sign in',
    submitting: 'Signing in…',
    invalidCredentials: 'Invalid credentials',
    missingFields: 'Enter your e-mail address and password.',
    unreachable: 'CloudFilm could not be reached. Try again.',
  },
};

/**
 * The contract every locale satisfies.
 *
 * Derived from {@link en} rather than declared separately, so there is exactly
 * one place a message can be added and no chance of a type and a catalogue
 * drifting apart.
 */
export type Messages = typeof en;

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
    recentEmptyBody:
      'Patients you open will be listed here once sign-in and access auditing are in place.',
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

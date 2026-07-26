/**
 * CloudFilm OHIF Viewer configuration.
 *
 * Mounted over the image's default `app-config.js`. OHIF itself is deployed
 * unmodified: CloudFilm customises behaviour through configuration and, later,
 * through OHIF's extension/mode API - never by forking the viewer.
 */
window.config = {
  routerBasename: '/',
  showStudyList: false,
  showWarningMessageForCrossOrigin: false,
  maxNumberOfWebWorkers: 4,

  // Studies are requested through this service's own nginx, which proxies to
  // Orthanc on the internal network. The browser therefore never learns an
  // address inside the DICOM tier.
  dataSources: [
    {
      namespace: '@ohif/extension-default.dataSourcesModule.dicomweb',
      sourceName: 'dicomweb',
      configuration: {
        friendlyName: 'CloudFilm Orthanc',
        name: 'orthanc',
        wadoUriRoot: '/dicom-web',
        qidoRoot: '/dicom-web',
        wadoRoot: '/dicom-web',
        qidoSupportsIncludeField: false,
        supportsReject: false,
        imageRendering: 'wadors',
        thumbnailRendering: 'wadors',
        enableStudyLazyLoad: true,
        supportsFuzzyMatching: false,
        omitQuotationForMultipartRequest: true,
      },
    },
  ],
  defaultDataSourceName: 'dicomweb',
};

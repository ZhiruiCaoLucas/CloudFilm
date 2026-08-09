/**
 * CloudFilm OHIF Viewer configuration.
 *
 * Mounted over the image's own `app-config.js` at
 * /usr/share/nginx/html/app-config.js. OHIF is deployed unmodified: CloudFilm
 * customises behaviour through configuration and, later, through OHIF's
 * extension/mode API - never by forking the viewer.
 *
 * Structure follows the upstream `platform/app/public/config/default.js`. When
 * this file replaces the image's default, it replaces it *entirely* - OHIF does
 * not merge it with anything - so every key `appInit` reads must be present here.
 */
window.config = {
  routerBasename: '/',

  /**
   * Additional extensions and modes to register at runtime.
   *
   * Empty is correct, and empty is what upstream's default.js ships. The
   * extensions and modes OHIF needs to function are compiled into the bundle
   * and registered by the application itself; these arrays are only for extra
   * ones supplied by a deployer.
   *
   * They must nevertheless be present. `appInit` iterates both, so omitting the
   * keys - as this file previously did - fails with
   * "TypeError: appConfig.extensions is not iterable" and a blank viewer.
   */
  extensions: [],
  modes: [],

  /**
   * Show the study list.
   *
   * A study the portal deep-links to but Orthanc does not hold then leaves the
   * user in a browsable viewer rather than on a blank screen. The workspace
   * still opens studies by URL; this is only a fallback.
   */
  showStudyList: true,

  /**
   * Studies are requested through this service's own nginx, which proxies
   * /dicom-web to Orthanc on the internal network. The browser therefore never
   * learns an address inside the DICOM tier.
   *
   * The flags below are set for Orthanc, which does not implement the
   * DCM4CHEE-oriented defaults upstream ships:
   *   qidoSupportsIncludeField  Orthanc does not support QIDO includefield.
   *   supportsReject            Rejection is a DCM4CHEE feature.
   *   supportsFuzzyMatching     Orthanc matches exactly.
   *   supportsWildcard          Left off; enable only once verified.
   */
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
        supportsWildcard: false,
        omitQuotationForMultipartRequest: true,
      },
    },
  ],

  defaultDataSourceName: 'dicomweb',

  /**
   * Reporting path for failed image retrievals.
   *
   * OHIF's cornerstone extension listens for IMAGE_LOAD_ERROR and does:
   *
   *   const handler = errorHandler.getHTTPErrorHandler();
   *   handler(detail.error);
   *
   * with no guard, while appInit resolves that getter to `undefined` unless
   * `httpErrorHandler` is defined here. Upstream's default.js defines it; this
   * file replaces default.js entirely, so omitting it turned every real image
   * load failure into "TypeError: handler is not a function" - thrown on the
   * exact line that would have reported which imageId failed and why.
   *
   * This restores the report. It is deliberately not a try/catch and not a
   * suppression: nothing about image loading changes, and errors that were
   * previously swallowed now reach the console with their detail intact.
   */
  httpErrorHandler: error => {
    console.error('[CloudFilm OHIF] HTTP error:', error);
  },
};

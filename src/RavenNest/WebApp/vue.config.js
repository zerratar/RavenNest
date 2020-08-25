module.exports = {
  outputDir: "../wwwroot/",
  filenameHashing: false,
  configureWebpack: {
    devtool: 'source-map'
  },
  chainWebpack: config => {
    config.performance
      .maxAssetSize(650000)
  }
}
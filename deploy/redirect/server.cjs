const http = require('node:http')
const destination = 'https://k-sweet-creations-co.azurewebsites.net'
http.createServer((request, response) => {
  if (request.method === 'GET' || request.method === 'HEAD') {
    response.writeHead(301, { Location: destination + request.url, 'Cache-Control': 'public, max-age=300' })
    response.end()
    return
  }
  response.writeHead(409, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' })
  response.end(JSON.stringify({ message: `The bakery website has moved. Reload the page to continue at ${destination}` }))
}).listen(Number(process.env.PORT || 8080), '0.0.0.0')

import { readFileSync } from 'node:fs'
import { createRequire } from 'node:module'
import { fileURLToPath } from 'node:url'

const root = new URL('./', import.meta.url)
const state = JSON.parse(readFileSync(new URL('deployment.local.json', root), 'utf8').replace(/^\uFEFF/, ''))
state.websiteUrl = process.env.AZURE_WEBSITE_URL || state.websiteUrl
const credentials = JSON.parse(readFileSync(new URL('credentials.local.json', root), 'utf8').replace(/^\uFEFF/, ''))
if (!state.websiteUrl) throw new Error('The Azure deployment has no verified website URL yet.')
const require = createRequire(new URL('../../K-Sweet-Kreations-Co-React-Website/package.json', root))
const { chromium } = require('@playwright/test')
async function checkPassword(page, label) {
  const input = page.getByLabel(label, { exact: true })
  const original = await input.inputValue()
  if (await input.getAttribute('type') !== 'password') throw new Error('A password field is not hidden by default.')
  await page.getByRole('button', { name: `Show ${label.toLowerCase()}`, exact: true }).click()
  if (await input.getAttribute('type') !== 'text' || await input.inputValue() !== original) throw new Error('Showing a password changed its value.')
  await page.getByRole('button', { name: `Hide ${label.toLowerCase()}`, exact: true }).press('Enter')
  if (await input.getAttribute('type') !== 'password' || await input.inputValue() !== original) throw new Error('Hiding a password changed its value.')
}
const browser = await chromium.launch({ headless: true })
let context
try {
  context = await browser.newContext()
  const page = await context.newPage()
  let productDeleteRequests = 0
  page.on('request', request => {
    if (request.method() === 'DELETE' && /\/api\/website\/products\/\d+$/.test(new URL(request.url()).pathname)) productDeleteRequests++
  })
  page.on('response', response => {
    if (response.status() >= 400 && new URL(response.url()).pathname.startsWith('/api/')) console.error(`Live API check: ${response.status()} ${new URL(response.url()).pathname}`)
    if (new URL(response.url()).pathname === '/api/Auth/login') console.log(`Live sign-in response: ${response.status()}`)
  })
  page.on('requestfailed', request => {
    if (new URL(request.url()).pathname.startsWith('/api/')) console.error(`Live API request interrupted: ${new URL(request.url()).pathname}`)
  })
  const health = await context.request.get(`${state.websiteUrl}/api/health`)
  if (health.status() !== 200 || (await health.json()).status !== 'healthy') throw new Error('Azure API/database health check failed.')
  await page.goto(`${state.websiteUrl}/#account`)
  await page.getByRole('button', { name: 'New here? Create an account', exact: true }).click()
  await checkPassword(page, 'Password')
  await checkPassword(page, 'Confirm password')
  await page.getByRole('button', { name: 'Already have an account? Sign in', exact: true }).click()
  await page.getByLabel('Email address').fill(credentials.adminEmail)
  await page.getByLabel('Password', { exact: true }).fill(credentials.adminPassword)
  await checkPassword(page, 'Password')
  await page.getByRole('button', { name: 'Sign in', exact: true }).click()
  await page.getByRole('heading', { name: 'Hello, bakery team.' }).waitFor({ timeout: 60000 })
  const session = (await context.cookies()).find(cookie => cookie.name === 'ksk_session')
  if (!session?.httpOnly || !session.secure || session.sameSite !== 'Strict') throw new Error('The live session cookie is not configured correctly.')
  const snapshotResponse = await context.request.get(`${state.websiteUrl}/api/website`)
  if (!snapshotResponse.ok()) throw new Error('Admin records could not be loaded.')
  const records = await snapshotResponse.json()
  if (!Array.isArray(records.customers) || !Array.isArray(records.staff)) throw new Error('Account directories are missing.')
  await page.getByRole('button', { name: 'Customers', exact: true }).click()
  await page.getByRole('heading', { name: `Registered customers (${records.customers.length})`, exact: true }).waitFor()
  for (const customer of records.customers) {
    const row = page.getByRole('row').filter({ has: page.getByRole('cell', { name: customer.email, exact: true }) })
    if (await row.count() !== 1) throw new Error('A registered customer is missing from the live directory.')
  }
  await page.getByRole('button', { name: 'Staff accounts', exact: true }).click()
  await checkPassword(page, 'Temporary password')
  await checkPassword(page, 'Confirm password')
  await page.screenshot({ path: fileURLToPath(new URL('artifacts/azure-admin.png', root)), fullPage: true })
  await page.getByRole('button', { name: 'Products', exact: true }).click()
  await page.getByRole('button', { name: 'Add product', exact: true }).click()
  const photograph = page.getByLabel('Product photograph (JPG/PNG/WebP, up to 2 MB)', { exact: true })
  if (await photograph.getAttribute('type') !== 'file' || await page.getByLabel('Product image path', { exact: true }).count()) throw new Error('The product form does not offer the expected image upload.')
  await page.getByRole('button', { name: 'Close details', exact: true }).click()
  if (records.products.length) {
    await page.getByRole('button', { name: 'Delete product', exact: true }).first().click()
    await page.getByRole('dialog').getByRole('heading', { name: 'Delete product?', exact: true }).waitFor()
    await page.getByRole('dialog').getByRole('button', { name: 'Cancel', exact: true }).click()
    if (productDeleteRequests !== 0) throw new Error('Cancelling the confirmation sent a product deletion request.')
  }
  for (const product of records.products.filter(p => p.image?.startsWith('/uploads/products/'))) {
    if (!(await context.request.get(`${state.websiteUrl}${product.image}`)).ok()) throw new Error('An existing product photograph is unavailable after migration.')
  }
  const privateFiles = [
    ...records.orders.flatMap(o => o.payments.map(p => p.proofUrl).filter(Boolean)),
    ...records.requests.map(r => r.image?.url).filter(Boolean),
  ]
  for (const path of privateFiles) {
    const file = await context.request.get(`${state.websiteUrl}${path}`)
    if (!file.ok() || !(await file.body()).length) throw new Error('An existing private upload is unavailable after migration.')
  }
  const logoutResponse = page.waitForResponse(response => response.url().endsWith('/api/Auth/logout') && response.request().method() === 'POST')
  await page.getByRole('button', { name: 'Sign out', exact: true }).click()
  if ((await logoutResponse).status() !== 204) throw new Error('Azure sign-out request failed.')
  const me = await context.request.get(`${state.websiteUrl}/api/Auth/me`)
  if (me.status() !== 401) throw new Error('Azure sign-out did not revoke the session.')
  console.log(`Azure checks passed: HTTPS, React, API/database, Admin sign-in, secure cookie, all ${records.customers.length} registered customers visible and sign-out.`)
  await context.close()
} catch (error) {
  // Playwright's full network errors include session headers; keep diagnostics private.
  console.error(String(error.message).split('\n')[0])
  process.exitCode = 1
} finally {
  if (context) {
    await context.request.post(`${state.websiteUrl}/api/Auth/logout`).catch(() => {})
    await context.close()
  }
  await browser.close()
}

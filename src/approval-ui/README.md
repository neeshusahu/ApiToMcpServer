# approval-ui

A React app for **approving or rejecting MCP tool calls** that ApiToMcpServer has paused for human review.

Approvers sign in with **WorkOS AuthKit**. The app sends each decision to **ApprovalApi**, using the access token AuthKit issued as a bearer token.

---

## WorkOS configuration

1. **Create an application**
   - Sign in to the [WorkOS Dashboard](https://dashboard.workos.com) and choose your environment, for example **Staging**.
   - Go to **Applications**, create a new application, and choose the client-side (single-page app) type.
   - Copy the application's **Client ID** (`client_…`).

2. **Enable AuthKit.** Under **Authentication**, turn on AuthKit and choose the sign-in methods you want, such as email + password, Google or magic link.

3. **Configure the redirect URI**
   - Go to **Redirects** and add `http://localhost:3000` under **Redirect URIs**.
   - Mark it as the **default**.
   - WorkOS only sends users back to URIs on this list after login.

4. **Allow the app's origin (CORS)**
   - Under **Authentication → Sessions → Cross-Origin Resource Sharing**, add `http://localhost:3000`.
   - The browser exchanges and refreshes tokens with WorkOS directly, so WorkOS has to allow this origin.

5. **Add the Client ID to the app.** Copy `.env.example` to `.env` in this folder and fill it in (`.env` is git-ignored):

   ```env
   REACT_APP_WORKOS_CLIENT_ID=client_xxxxxxxxxxxxxxxxxxxxxxxxxx
   ```

   ApprovalApi must use the **same Client ID** in its `WorkOS:ClientId` setting.

For production, repeat steps 3 and 4 with your real domain.

---

## Run

```bash
npm install
npm start          # http://localhost:3000
```

ApprovalApi needs to be running on `http://localhost:5282`. If you edit `.env`, restart `npm start` to pick up the change.

---

## Routes

| Route | Sign-in required | Purpose |
|---|---|---|
| `/` | No | Shows the signed-in user, with Sign in / Sign out |
| `/login` | No | Starts the AuthKit sign-in right away |
| `/approval/:id` | **Yes** | Approve or reject an approval |

---

## How AuthKit is used

The app uses [`@workos-inc/authkit-react`](https://github.com/workos/authkit-react).

**1. Provider (`src/index.js`).** `AuthKitProvider` wraps the app. It handles the redirect back from WorkOS and keeps the session.

```jsx
<AuthKitProvider
  clientId={process.env.REACT_APP_WORKOS_CLIENT_ID}
  onRedirectCallback={({ state }) => {
    if (state?.returnTo) window.location.replace(state.returnTo);
  }}
>
  <App />
</AuthKitProvider>
```

**2. Sign-in (`src/Login/RequireAuth.js`).** When someone opens an approval link without being signed in, the app sends them to the AuthKit login page. After login, it brings them back to that same approval.

```js
signIn({ state: { returnTo: location.pathname + location.search } });
```

**3. Calling the API (`src/Approval/ApprovalPage.js`).** The page gets the access token and sends it with the request:

```js
const accessToken = await getAccessToken();

await fetch(`http://localhost:5282/approval/${id}?approvalStatus=${status}`, {
  method: "POST",
  headers: { Authorization: `Bearer ${accessToken}` },
});
```

`approvalStatus` is `2` for **Approved** and `3` for **Rejected**. If the session has expired, the page asks the user to sign in again.

---

## Tokens issued by AuthKit

When the user signs in, AuthKit issues a **session** made up of two tokens:

| Token | What it is | Where it's kept |
|---|---|---|
| **Access token** | Short-lived JWT, about 5 minutes by default. Sent to the API as `Authorization: Bearer <token>`. | In memory |
| **Refresh token** | Long-lived token for the session. AuthKit uses it to get a new access token when the old one expires. | `localStorage` in development; a secure cookie in production |

`getAccessToken()` always returns a valid access token. It refreshes the token automatically when needed.

### Access token claims

| Claim | Meaning |
|---|---|
| `sub` | WorkOS user ID. The API uses it to identify the approver. |
| `sid` | Session ID |
| `iss` | Issuer: `https://api.workos.com/user_management/<clientId>` |
| `exp` / `iat` | Expiry and issued-at times |
| `org_id`, `role`, `permissions` | Present when the user belongs to an organization |

The token doesn't include profile fields such as email and name by default. You can add them with a **JWT template** in the WorkOS Dashboard if needed.

---

## Scripts

| Command | Description |
|---|---|
| `npm start` | Dev server on http://localhost:3000 |
| `npm run build` | Production build into `build/` |

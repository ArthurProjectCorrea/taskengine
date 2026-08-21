/**
 * TaskEngine OAuth token-exchange proxy.
 *
 * GitHub always requires `client_secret` when exchanging an authorization code for an access
 * token, even for a PKCE flow (unlike Google/Microsoft) - see
 * https://github.blog/changelog/2025-07-14-pkce-support-for-oauth-and-github-app-authentication/
 * and
 * https://docs.github.com/en/apps/oauth-apps/maintaining-oauth-apps/troubleshooting-oauth-app-access-token-request-errors.
 * Embedding that secret in the distributed desktop binary would make it trivially extractable, so
 * this tiny stateless Worker holds it instead (as a Cloudflare secret, never in source) and does
 * the code-for-token exchange on the desktop app's behalf. The app only ever sees the final
 * access token.
 *
 * The initial authorization step (redirecting the user to github.com/login/oauth/authorize) does
 * NOT go through this proxy - only the token exchange does. See
 * TaskEngine.Infrastructure/Providers/GitHub/GitHubOAuthAuthenticator.cs on the desktop side.
 */

export interface Env {
  GITHUB_CLIENT_ID: string;
  GITHUB_CLIENT_SECRET: string;
}

interface TokenExchangeRequestBody {
  code?: string;
  codeVerifier?: string;
  redirectUri?: string;
}

const GITHUB_TOKEN_ENDPOINT = "https://github.com/login/oauth/access_token";

/**
 * Cheap defense against this proxy being used as an open relay for arbitrary apps: the
 * client_secret is fixed to a single GitHub App, so this check doesn't prevent all misuse, but it
 * does rule out the proxy being pointed at some other app's custom redirect scheme.
 */
const ALLOWED_REDIRECT_URI_PREFIX = "http://127.0.0.1:";

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    const url = new URL(request.url);

    if (request.method === "POST" && url.pathname === "/github/token") {
      return handleTokenExchange(request, env);
    }

    return new Response("Not found", { status: 404 });
  },
};

async function handleTokenExchange(request: Request, env: Env): Promise<Response> {
  let body: TokenExchangeRequestBody;
  try {
    body = await request.json();
  } catch {
    return jsonResponse({ error: "invalid_request", error_description: "Body must be valid JSON." }, 400);
  }

  const { code, codeVerifier, redirectUri } = body;

  if (!code || !codeVerifier || !redirectUri) {
    return jsonResponse(
      {
        error: "invalid_request",
        error_description: "code, codeVerifier and redirectUri are all required.",
      },
      400,
    );
  }

  if (!redirectUri.startsWith(ALLOWED_REDIRECT_URI_PREFIX)) {
    return jsonResponse(
      {
        error: "invalid_request",
        error_description: `redirectUri must start with '${ALLOWED_REDIRECT_URI_PREFIX}'.`,
      },
      400,
    );
  }

  const githubResponse = await fetch(GITHUB_TOKEN_ENDPOINT, {
    method: "POST",
    headers: {
      "Content-Type": "application/json",
      Accept: "application/json",
    },
    body: JSON.stringify({
      client_id: env.GITHUB_CLIENT_ID,
      client_secret: env.GITHUB_CLIENT_SECRET,
      code,
      redirect_uri: redirectUri,
      code_verifier: codeVerifier,
    }),
  });

  let githubBody: unknown;
  try {
    githubBody = await githubResponse.json();
  } catch {
    return jsonResponse(
      { error: "bad_gateway", error_description: "GitHub returned a non-JSON response." },
      502,
    );
  }

  // Repassa a resposta do GitHub como veio (access_token/token_type/scope no sucesso,
  // error/error_description na falha) com o mesmo status code - nada do client_secret entra
  // nessa resposta, o GitHub nunca o ecoa de volta.
  return jsonResponse(githubBody, githubResponse.status);
}

function jsonResponse(body: unknown, status: number): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });
}

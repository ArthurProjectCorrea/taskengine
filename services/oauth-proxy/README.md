# taskengine-oauth-proxy

Cloudflare Worker que troca um `code` de autorização OAuth do GitHub por um `access_token`, em
nome do app desktop TaskEngine.

## Por que existe

O GitHub exige `client_secret` na troca do código de autorização por token, mesmo em um fluxo
Authorization Code + PKCE (diferente de Google/Microsoft) - ver
[changelog do GitHub](https://github.blog/changelog/2025-07-14-pkce-support-for-oauth-and-github-app-authentication/)
e a
[documentação de troubleshooting](https://docs.github.com/en/apps/oauth-apps/maintaining-oauth-apps/troubleshooting-oauth-app-access-token-request-errors).
Embutir esse `client_secret` no `.exe` distribuído seria inseguro (extraível por qualquer um). Este
Worker guarda o secret como variável de ambiente do Cloudflare, faz a troca com o GitHub, e devolve
só o resultado (`access_token`/`token_type`/`scope`, ou o erro) pro app desktop - que nunca vê nem
guarda o secret.

O app desktop continua indo direto para `github.com/login/oauth/authorize` na etapa de autorização
inicial (o `client_id` é público, vai na URL do navegador de qualquer forma). Só a troca de código
por token passa por esta ponte.

## Rota

`POST /github/token`

Corpo (JSON):

```json
{
  "code": "...",
  "codeVerifier": "...",
  "redirectUri": "http://127.0.0.1:PORT/callback/"
}
```

`redirectUri` precisa começar com `http://127.0.0.1:` (checagem simples contra uso como proxy
aberto para outro app - não é uma defesa completa, já que o `client_secret` é fixo do GitHub App do
Arthur, mas é barata e vale a pena).

Resposta: repassa o corpo e o status code que o GitHub devolveu em
`POST https://github.com/login/oauth/access_token` (sucesso: `access_token`/`token_type`/`scope`;
erro: `error`/`error_description`).

Qualquer outra rota ou método devolve 404.

## Rodar localmente

```sh
npm install
npm run dev
```

Isso sobe o Worker em `http://localhost:8787` via `wrangler dev`. Para testar localmente com
secrets reais sem configurá-los no Cloudflare, crie um arquivo `.dev.vars` nesta pasta (já
ignorado pelo git):

```
GITHUB_CLIENT_ID=...
GITHUB_CLIENT_SECRET=...
```

## Configurar os secrets no Cloudflare

Rodado interativamente por quem tem acesso à conta Cloudflare - os valores nunca devem ser
compartilhados em texto puro com ninguém (incluindo em chat com IA):

```sh
npx wrangler secret put GITHUB_CLIENT_ID
npx wrangler secret put GITHUB_CLIENT_SECRET
```

Cada comando pede o valor via prompt interativo do terminal.

## Implantar

```sh
npx wrangler deploy
```

Devolve a URL final do Worker, algo como `https://taskengine-oauth-proxy.<subdomínio>.workers.dev`.
Essa URL precisa ser configurada como `TokenExchangeProxyUrl` em
`src/Frontend/TaskEngine.Desktop/MauiProgram.cs` (procure pelo comentário `TODO` perto do registro
de `GitHubOAuthOptions`), substituindo o placeholder atual.

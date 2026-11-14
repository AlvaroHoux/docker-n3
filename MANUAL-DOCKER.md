# Manual passo a passo — Dockerizando os 3 sistemas (Projeto N3)

**Integração de Aplicações — Engenharia de Software — UniRV**
Prof. Esp. Elielton Monteiro

> Este manual te guia, passo a passo, na criação dos contêineres. **Ele não entrega tudo pronto.** O Sistema 1 (Cadastro) vem explicado linha por linha. Os Sistemas 2 e 3 você monta sozinho, seguindo o mesmo modelo. No `docker-compose.yml` você completa as lacunas marcadas com `???`.

---

## 0. O que você recebeu

Você recebeu **3 sistemas que funcionam, mas que ainda não estão em contêineres**. Cada sistema tem uma API (.NET 10) e uma tela (React):

| Sistema | O que faz | API (.NET) | Tela (React) | Dados |
|---|---|---|---|---|
| **1 – Cadastro** | Cadastra funcionários (nome, cargo, valor da hora) | `sistemas/cadastro/api` – porta **5001** | `sistemas/cadastro/web` – porta **3001** | `funcionarios.json` |
| **2 – Ponto** | Busca os funcionários no Sistema 1 e lança a frequência (entrada/saída) | `sistemas/ponto/api` – porta **5002** | `sistemas/ponto/web` – porta **3002** | `ponto.json` |
| **3 – Folha** | Busca os dados nos Sistemas 1 e 2 e gera a folha de ponto (dias, horas, valor) | `sistemas/folha/api` – porta **5003** | `sistemas/folha/web` – porta **3003** | não tem; só consulta os outros |

Como eles conversam:

```
   NAVEGADOR (seu PC)
     │            │              │
     ▼            ▼              ▼
 cadastro-web  ponto-web     folha-web        (React servido pelo Nginx)
     │            │              │
     ▼            ▼              ▼
 cadastro-api ◄── ponto-api ◄── folha-api     (.NET)
     │   ▲                         │
     │   └─────────────────────────┘
     ▼            ▼
 funcionarios.json  ponto.json                (persistência em arquivo JSON)
```

**Ao final você terá 6 contêineres:** 3 APIs e 3 telas.

### Endpoints úteis (para testar no navegador ou com `curl`)

| Sistema | Endpoint | Para quê |
|---|---|---|
| Todos | `GET /health` | Mostra se a API está viva (e, no Ponto e na Folha, se as dependências estão no ar) |
| Todos | `GET /api/info` | Mostra a **máquina** onde a API roda (no Docker, é o ID do contêiner) |
| Todos | `POST /api/admin/derrubar` | **Derruba a API de propósito** (para o teste de resiliência) |
| Cadastro | `GET/POST /api/funcionarios`, `POST /api/seed` | Lista/cadastra funcionários; `seed` cria 5 de exemplo |
| Ponto | `GET/POST /api/ponto`, `POST /api/ponto/gerar` | Lista/lança marcações; `gerar` cria 30 dias úteis |
| Folha | `GET /api/folha/{id}?inicio=AAAA-MM-DD&fim=AAAA-MM-DD`, `GET /api/folha` | Folha de um funcionário / resumo de todos |

> No **PowerShell**, use `curl.exe` (com `.exe`), não `curl`. Exemplo: `curl.exe -X POST http://localhost:5001/api/seed`

---

## 1. Preparar o ambiente

1. Instale o **Docker Desktop** (https://www.docker.com/products/docker-desktop). No Windows, aceite usar o **WSL 2**.
2. Abra o Docker Desktop e espere aparecer **"Engine running"** (canto inferior esquerdo).
3. Abra um terminal (PowerShell ou o terminal do VS Code) e confira:

```bash
docker --version
docker compose version
docker run hello-world
```

Se aparecer `Hello from Docker!`, está tudo certo. ✅

> **Opcional:** se você tiver o **.NET 10 SDK** e o **Node 20+** instalados, dá para rodar os sistemas sem Docker (veja o `README.md`). Isso não é obrigatório: o objetivo do trabalho é justamente rodar **sem instalar nada disso**, só com Docker.

---

## 2. Conceitos rápidos (você vai explicar isso no vídeo!)

| Termo | Em uma frase |
|---|---|
| **Imagem** | "Pacote" somente-leitura com o sistema e tudo o que ele precisa. Como uma "classe". |
| **Contêiner** | Uma imagem **rodando**. Como um "objeto" criado da classe. |
| **Dockerfile** | A receita que diz como montar a imagem. |
| **Multi-stage build** | Um Dockerfile com 2 etapas: uma imagem grande para **compilar** e uma pequena para **rodar**. |
| **Porta publicada** (`-p 5001:8080`) | Liga a porta **5001 do seu PC** à porta **8080 de dentro do contêiner**. |
| **Volume / bind mount** | Pasta do seu PC "montada" dentro do contêiner. Os dados **sobrevivem** quando o contêiner é apagado. |
| **Rede do compose** | Os contêineres do mesmo `docker-compose.yml` se enxergam **pelo nome do serviço** (ex.: `http://cadastro-api:8080`). |
| **`docker stop`** | Desliga o contêiner (o processo termina). |
| **`docker pause`** | **Congela** o contêiner. O processo continua existindo, mas não responde nada. |

---

## 3. Sistema 1 — API do Cadastro (explicado linha por linha)

### 3.1 Crie o arquivo `sistemas/cadastro/api/Dockerfile`

O arquivo se chama **`Dockerfile`**, sem extensão.

```dockerfile
# ---------- Etapa 1: compilar (imagem grande, com o SDK) ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---------- Etapa 2: rodar (imagem pequena, só o runtime) ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV DATA_DIR=/app/dados
EXPOSE 8080
ENTRYPOINT ["dotnet", "CadastroApi.dll"]
```

O que cada linha faz:

| Linha | Explicação |
|---|---|
| `FROM ...sdk:10.0 AS build` | Começa de uma imagem que tem o **SDK do .NET** (o compilador). Damos o apelido `build` para essa etapa. |
| `WORKDIR /src` | Cria a pasta `/src` dentro da imagem e entra nela (é como um `cd`). |
| `COPY *.csproj ./` | Copia **só o arquivo do projeto** primeiro. |
| `RUN dotnet restore` | Baixa os pacotes NuGet. Como o `.csproj` quase nunca muda, o Docker **reaproveita essa camada (cache)** nos próximos builds. |
| `COPY . .` | Agora copia o resto do código. |
| `RUN dotnet publish ...` | Compila em modo Release e coloca o resultado em `/app/publish`. |
| `FROM ...aspnet:10.0` | **Segunda etapa:** começa de novo, de uma imagem **bem menor**, que só tem o runtime (sem compilador). |
| `COPY --from=build ...` | Traz da etapa `build` apenas os arquivos já compilados. |
| `ENV DATA_DIR=/app/dados` | Diz à API onde salvar o `funcionarios.json`. (Leia `JsonStore.cs`: ele usa essa variável.) |
| `EXPOSE 8080` | Documenta que a API escuta na porta 8080. A imagem `aspnet` já define `ASPNETCORE_HTTP_PORTS=8080`. |
| `ENTRYPOINT [...]` | Comando que roda quando o contêiner liga. |

### 3.2 Crie o arquivo `sistemas/cadastro/api/.dockerignore`

Ele evita que lixo de compilação vá para dentro da imagem:

```
bin/
obj/
dados/
```

### 3.3 Construa a imagem e rode o contêiner

No terminal, **entre na pasta da API**:

```bash
cd sistemas/cadastro/api
docker build -t cadastro-api .
```

> O `.` no final significa "use a pasta atual como contexto". O `-t` dá um nome (tag) à imagem.

Agora rode:

```bash
docker run -d --name cadastro-api -p 5001:8080 cadastro-api
```

| Parte | Significado |
|---|---|
| `-d` | Roda em segundo plano (*detached*). |
| `--name cadastro-api` | Nome do contêiner. |
| `-p 5001:8080` | Porta **5001 do PC** → porta **8080 do contêiner**. |

Teste no navegador: **http://localhost:5001/api/info**

Repare no campo `"maquina"`: é o **ID do contêiner**. Compare com:

```bash
docker ps
```

Crie os 5 funcionários de exemplo e liste:

```bash
curl.exe -X POST http://localhost:5001/api/seed
curl.exe http://localhost:5001/api/funcionarios
```

### 3.4 🔎 Experimento: onde foram parar os dados?

```bash
docker rm -f cadastro-api
docker run -d --name cadastro-api -p 5001:8080 cadastro-api
curl.exe http://localhost:5001/api/funcionarios
```

**A lista voltou vazia!** O arquivo JSON estava **dentro** do contêiner, e ele foi apagado. Para resolver isso, usamos um **volume**:

```bash
docker rm -f cadastro-api
docker run -d --name cadastro-api -p 5001:8080 -v "${PWD}/dados:/app/dados" cadastro-api
```

> `-v pasta_do_PC:pasta_do_contêiner`. No PowerShell, `${PWD}` é a pasta atual. No CMD, use `%cd%`. No Git Bash/Linux, use `$(pwd)`.

Rode o `seed` de novo, apague o contêiner, crie outro e veja que **agora os dados ficam**. Abra a pasta `dados/` no VS Code: o `funcionarios.json` está lá!

📸 **CHECKPOINT 1:** tire um print do navegador em `http://localhost:5001/api/info` **e** do `docker ps` no mesmo print, mostrando que o ID do contêiner é igual ao campo `maquina`.

---

## 4. Sistema 1 — Tela do Cadastro (React + Nginx)

O React precisa ser **compilado** (`npm run build`), o que gera uma pasta `dist/` com HTML, CSS e JS. Depois disso **não precisamos mais do Node**: basta um servidor web simples. Vamos usar o **Nginx**.

### 4.1 Crie `sistemas/cadastro/web/Dockerfile`

```dockerfile
# ---------- Etapa 1: build do React (Node) ----------
FROM node:22-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
ARG VITE_API_URL
ENV VITE_API_URL=$VITE_API_URL
RUN npm run build

# ---------- Etapa 2: servir os arquivos estáticos (Nginx) ----------
FROM nginx:1.27-alpine
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
```

| Linha | Explicação |
|---|---|
| `npm ci` | Instala exatamente as versões do `package-lock.json`. |
| `ARG VITE_API_URL` | Um **argumento de build**: valor que você passa na hora do `docker build`. |
| `ENV VITE_API_URL=...` | Repassa o argumento para o Vite, que **grava o endereço da API dentro do JavaScript** durante o build. |
| `FROM nginx...` | A imagem final só tem o Nginx e os arquivos estáticos (fica com uns ~50 MB). |

### 4.2 Crie `sistemas/cadastro/web/.dockerignore`

```
node_modules/
dist/
```

### 4.3 Construa e rode

```bash
cd ../web
docker build -t cadastro-web --build-arg VITE_API_URL=http://localhost:5001 .
docker run -d --name cadastro-web -p 3001:80 cadastro-web
```

Abra **http://localhost:3001**. A bolinha no topo deve estar **verde** ("API online").

### ❓ Pergunta importante (responda no vídeo)

> Por que o front usa `http://localhost:5001`, e não o nome do contêiner?

Porque **quem chama a API é o navegador**, que roda no **seu PC**, fora do Docker. O contêiner do Nginx só entrega os arquivos; ele não chama a API. Para o navegador, a API está em `localhost:5001` (a porta publicada).

Quando terminar, remova os contêineres manuais (a partir de agora vamos usar o compose):

```bash
docker rm -f cadastro-api cadastro-web
```

---

## 5. Sistemas 2 e 3 — agora é com você

Crie os Dockerfiles e `.dockerignore` das outras 4 pastas, **seguindo os modelos acima**:

| Pasta | Baseie-se em | O que muda |
|---|---|---|
| `sistemas/ponto/api` | Dockerfile da API do Cadastro | O nome da DLL (veja o nome do `.csproj`) |
| `sistemas/folha/api` | Dockerfile da API do Cadastro | O nome da DLL. A Folha **não grava arquivo**: a linha do `DATA_DIR` é necessária? |
| `sistemas/ponto/web` | Dockerfile da tela do Cadastro | Nada no arquivo; muda o valor do `VITE_API_URL` no compose |
| `sistemas/folha/web` | Dockerfile da tela do Cadastro | Idem |

> Dica: leia o `Program.cs` do Ponto e da Folha. Procure por `Environment.GetEnvironmentVariable`. Essas são as **variáveis de ambiente** que você vai precisar configurar no compose.

---

## 6. Juntando tudo com o Docker Compose

Subir 6 contêineres na mão, com `docker run`, é trabalhoso. O **Docker Compose** descreve todos eles em **um único arquivo** e sobe tudo com **um comando**.

### 6.1 Crie `sistemas/docker-compose.yml` e complete as lacunas `???`

```yaml
services:

  # ---------------- SISTEMA 1 - CADASTRO ----------------
  cadastro-api:
    build: ./cadastro/api
    container_name: cadastro-api
    ports:
      - "5001:8080"                    # porta_do_PC:porta_do_contêiner
    volumes:
      - ./dados/cadastro:/app/dados    # o funcionarios.json fica visível no PC
    restart: unless-stopped

  cadastro-web:
    build:
      context: ./cadastro/web
      args:
        VITE_API_URL: http://localhost:5001   # quem chama a API é o NAVEGADOR
    container_name: cadastro-web
    ports:
      - "3001:80"
    depends_on:
      - cadastro-api
    restart: unless-stopped

  # ---------------- SISTEMA 2 - PONTO ----------------
  ponto-api:
    build: ???
    container_name: ponto-api
    ports:
      - "???:8080"
    environment:
      CADASTRO_API_URL: http://???:8080   # contêiner fala com contêiner pelo NOME do serviço
    volumes:
      - ???:/app/dados
    depends_on:
      - ???
    restart: unless-stopped

  ponto-web:
    ???   # monte igual ao cadastro-web (porta 3002, API em 5002)

  # ---------------- SISTEMA 3 - FOLHA ----------------
  folha-api:
    ???   # porta 5003; precisa de DUAS variáveis: CADASTRO_API_URL e PONTO_API_URL

  folha-web:
    ???   # porta 3003, API em 5003
```

### 6.2 Suba tudo

Na pasta `sistemas/` (onde está o `docker-compose.yml`):

```bash
docker compose up -d --build
docker compose ps
```

Você deve ver **6 contêineres** com status `Up` (ou `running`).

Abra as três telas:

- http://localhost:3001 → cadastre funcionários (ou clique em **"Criar 5 de exemplo"**)
- http://localhost:3002 → escolha um funcionário e lance pontos, ou clique em **"Gerar 30 dias úteis"**
- http://localhost:3003 → escolha o funcionário e o período e clique em **"Gerar folha"**

### 6.3 Comandos que você vai usar o tempo todo

| Comando | Para quê |
|---|---|
| `docker compose ps` | Ver o estado dos contêineres |
| `docker compose logs -f ponto-api` | Ver os logs ao vivo de um serviço (Ctrl+C para sair) |
| `docker compose stop cadastro-api` / `start` | Desligar / ligar **um** serviço |
| `docker compose pause cadastro-web` / `unpause` | Congelar / descongelar um serviço |
| `docker compose restart folha-api` | Reiniciar um serviço |
| `docker compose up -d --build` | Recompilar e subir de novo depois de mudar algo |
| `docker compose down` | Desligar e **apagar** os contêineres (os dados em `./dados` continuam) |
| `docker network ls` / `docker network inspect sistemas_default` | Ver a rede que o compose criou |
| `docker stats` | Ver CPU e memória de cada contêiner |
| `docker images` | Ver as imagens e o **tamanho** de cada uma |

📸 **CHECKPOINT 2:** print do `docker compose ps` com os 6 contêineres no ar **e** da tela da Folha (3003) mostrando uma folha gerada.

---

## 7. Testes que devem aparecer no vídeo

Para cada teste: **mostre o comando**, **mostre o que aconteceu** (tela + terminal/logs) e **explique por quê**. Os testes marcados com ⭐ são **obrigatórios**.

> Dica de gravação: deixe um terminal aberto com `docker compose logs -f ponto-api folha-api` enquanto faz os testes. Assim os erros aparecem ao vivo.

### ⭐ T1 — Tudo no ar e conversando

```bash
docker compose ps
```

Faça o fluxo completo: cadastrar → lançar ponto → gerar folha. Abra `http://localhost:5002/health` e `http://localhost:5003/health` e mostre `"cadastro": "ok"` e `"ponto": "ok"`.

### ⭐ T2 — Pausar o FRONT-END (`docker pause`)

```bash
docker compose pause cadastro-web
```

- Recarregue `http://localhost:3001` (F5) → a página **fica carregando sem parar**.
- Agora abra `http://localhost:5001/api/funcionarios` → **a API continua respondendo!**
- O Ponto (3002) e a Folha (3003) **continuam funcionando** e continuam enxergando os funcionários.

```bash
docker compose unpause cadastro-web
```

**Explique:** o front é só a "vitrine". Pausar o front não afeta a API nem os dados, nem os outros sistemas. O `pause` congela o processo, que não responde mas também não recusa a conexão; por isso o navegador fica esperando.

### ⭐ T3 — Desligar o FRONT-END (`docker stop`)

```bash
docker compose stop cadastro-web
```

Recarregue `http://localhost:3001` → erro **imediato** ("Não é possível acessar esse site" / `ERR_CONNECTION_REFUSED`). A API (5001) continua funcionando.

```bash
docker compose start cadastro-web
```

**Explique a diferença entre `pause` (fica esperando) e `stop` (recusa na hora).**

### ⭐ T4 — Desligar a API: o front-end cai?

```bash
docker compose stop cadastro-api
```

Observe e mostre:

1. **Tela do Cadastro (3001):** ela **NÃO cai**. A página continua abrindo (quem a serve é o Nginx), mas a bolinha fica **vermelha ("API OFFLINE")** e aparece a mensagem *"Não foi possível conectar na API..."*.
2. **Tela do Ponto (3002):** clique em **"Recarregar funcionários"** → erro (o Ponto depende do Cadastro). Mas clique em **"Recarregar marcações"** → **as marcações continuam aparecendo**, porque elas estão no `ponto.json`, que é do Sistema 2. Tente lançar um ponto → erro **503** ("Sistema de Cadastro indisponível... Name or service not known" ou "não respondeu em 5 segundos"). Curiosidade: quando um contêiner é parado, o **nome dele some do DNS** da rede do compose. Por isso aparece "Name or service not known".
3. **Tela da Folha (3003):** a bolinha mostra *"API no ar, mas sem: cadastro"*. Gerar folha → erro 503.
4. **Logs:** `docker compose logs ponto-api` mostra o aviso *"Dependência indisponível"*.

```bash
docker compose start cadastro-api
```

Tudo volta, **sem perder dados**.

**Explique:** a falha de um serviço se **propaga** para quem depende dele (cascata), mas cada sistema **trata o erro** e não "quebra" por inteiro. Isso é **degradação parcial**.

### T5 — Pausar a API (`docker pause`) × desligar (`docker stop`)

```bash
docker compose pause cadastro-api
```

No Ponto (3002), clique em **"Recarregar funcionários"** e **conte o tempo**: leva cerca de **5 segundos** até aparecer *"não respondeu em 5 segundos... está pausado ou travado?"*.

```bash
docker compose unpause cadastro-api
```

**Explique:** um serviço **travado** é pior que um serviço **desligado**, porque quem chama fica esperando. Por isso as APIs usam **timeout** (procure `Timeout = TimeSpan.FromSeconds(5)` no `Program.cs` do Ponto).

### ⭐ T6 — Persistência inviabilizada (JSON corrompido) — exigido na especificação

Com tudo no ar, abra no VS Code o arquivo `sistemas/dados/ponto/ponto.json` e **estrague o conteúdo** (apague um `]` ou escreva qualquer texto no começo). Salve.

- Tela do Ponto (3002) → **"Recarregar marcações"** → erro 500 *"O arquivo ... ponto.json está corrompido (JSON inválido)"*.
- `http://localhost:5002/health` → também acusa erro.
- A Folha (3003) → *"Sistema de Ponto respondeu com erro 500"*.
- **O Cadastro (3001) continua 100% normal**: cada sistema tem a sua persistência.

Conserte o arquivo (Ctrl+Z e salve) → tudo volta **sem reiniciar nada**.

Variação: **apague** o `ponto.json`. O que acontece? (Dica: leia o `JsonStore.cs`. Os dados voltam?)

### ⭐ T7 — Os dados sobrevivem? (volume)

```bash
docker compose down
docker compose up -d
```

Os funcionários e as marcações **continuam lá** (estão em `./dados`, no seu PC).

Agora **comente** (`#`) as linhas de `volumes:` do `cadastro-api` no compose e rode `docker compose up -d` de novo. Cadastre alguém e então rode `docker compose down` e `up -d`. O que aconteceu? **Volte as linhas** depois.

### ⭐ T8 — Queda inesperada e reinício automático (`restart: unless-stopped`)

```bash
curl.exe -X POST http://localhost:5001/api/admin/derrubar
docker compose ps
```

Rode o `ps` algumas vezes: o contêiner cai (código de saída 1) e o **Docker o liga de novo sozinho**. Veja o histórico em `docker compose logs cadastro-api`.

Compare com `docker compose stop cadastro-api`: nesse caso ele **não** volta sozinho. Por quê? (Dica: leia sobre `unless-stopped`.)

### ⭐ T9 — Rede: por que `localhost` não funciona entre contêineres?

No compose, troque a variável do `ponto-api` para:

```yaml
CADASTRO_API_URL: http://localhost:5001
```

Rode `docker compose up -d` e tente carregar os funcionários no Ponto. **Quebrou!**

**Explique:** dentro de um contêiner, `localhost` é **o próprio contêiner**, e não o seu PC. Entre contêineres, use o **nome do serviço** (`cadastro-api`) e a **porta interna** (`8080`). Mostre `docker network inspect sistemas_default` com os 6 contêineres na mesma rede. **Desfaça a alteração.**

### T10 — Tamanho das imagens (multi-stage)

```bash
docker images
```

Compare o tamanho de `mcr.microsoft.com/dotnet/sdk:10.0` com o da imagem final da sua API, e o de `node:22-alpine` com o da imagem final do front. **Explique por que o multi-stage build deixa a imagem final menor.**

### Testes bônus (contam a favor)

- **B1 — Healthcheck:** adicione `healthcheck:` no compose para as APIs e use `depends_on: condition: service_healthy`. (Atenção: a imagem `aspnet` não vem com `curl`; pesquise como resolver.)
- **B2 — Limite de recursos:** use `deploy.resources.limits.memory: 128M` em uma API e mostre o limite com `docker stats`.
- **B3 — Rede isolada:** crie duas redes (`frontend` e `backend`) e mostre que a folha-api enxerga as outras APIs, mas as telas não precisam estar na rede do backend.
- **B4 — Volume nomeado × bind mount:** troque `./dados/cadastro` por um volume nomeado e explique a diferença.

---

## 8. Problemas comuns

| Sintoma | Causa provável / solução |
|---|---|
| `port is already allocated` | Já tem algo usando a porta (talvez um `docker run` antigo). Rode `docker ps -a` e `docker rm -f <nome>`. |
| `Cannot connect to the Docker daemon` / `pipe ... dockerDesktopLinuxEngine` | O Docker Desktop não está aberto. Abra e espere "Engine running". |
| Front abre, mas a bolinha fica sempre vermelha | O `VITE_API_URL` está errado. Ele é gravado **no build**: corrija e rode `docker compose up -d --build`. |
| Ponto mostra "Sistema de Cadastro indisponível" com tudo no ar | Erro no `CADASTRO_API_URL` (use `http://cadastro-api:8080`, e não `localhost`). |
| `npm ci` falha no build | Falta o `package-lock.json` na pasta, ou a pasta `node_modules` foi copiada (confira o `.dockerignore`). |
| `COPY failed` / arquivo não encontrado | Você rodou o `docker build` na pasta errada; o contexto é o `.` (a pasta atual). |
| Mudei o código e nada mudou | Faltou o `--build`: `docker compose up -d --build`. |

---

## 9. Checklist final antes de entregar

- [ ] 6 `Dockerfile` + 6 `.dockerignore`
- [ ] `docker-compose.yml` completo, na pasta `sistemas/`
- [ ] Com o projeto **zerado** (`docker compose down`, apague a pasta `dados/` e as imagens), o comando `docker compose up -d --build` sobe **tudo** sem erro
- [ ] `README.md` explicando como rodar, as portas e os testes
- [ ] Vídeo de no máximo 10 minutos com os testes ⭐
- [ ] Slides em PDF

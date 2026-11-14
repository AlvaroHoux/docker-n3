# Projeto N3 — Arquitetura Distribuída com Docker

**Integração de Aplicações — Engenharia de Software, 6º período — UniRV**
Prof. Esp. Elielton Monteiro

> **RETIFICAÇÃO (13/11/2026):** os grupos podem ter **até 4 pessoas** (e não 2, como dizia a versão anterior). A seção 2 e os itens que falavam em "dupla" foram corrigidos.

---

## 1. Objetivo

Você recebeu **três sistemas independentes** que se comunicam pela rede (HTTP/JSON):

1. **Cadastro de Funcionários**: nome, cargo e valor da hora.
2. **Controle de Ponto**: busca os funcionários no Sistema 1 e registra a frequência (entrada/saída), com geração automática de **30 dias úteis**.
3. **Folha de Ponto**: consulta o Sistema 1 (valor da hora) e o Sistema 2 (dias trabalhados) e gera a **folha de ponto consolidada** de cada funcionário (dias, horas e valor bruto).

Cada sistema tem uma **API em .NET 10** e uma **tela em React**. A persistência é feita em **arquivos JSON**.

Os sistemas **funcionam**, mas foram entregues **"desmodularizados"**: não existe nenhum Dockerfile nem docker-compose. **O seu trabalho é a parte de infraestrutura:**

- empacotar cada parte em **seu próprio contêiner** (6 contêineres no total);
- orquestrar tudo com **Docker Compose**;
- **provar, com testes, como a arquitetura se comporta quando partes dela falham**;
- explicar tudo isso em um **vídeo** e em **slides**.

O passo a passo está no arquivo **`MANUAL-DOCKER.md`**.

> Você **não precisa** alterar o código C# nem o React. Se alterar (melhorias, bônus), explique no README e no vídeo.

---

## 2. Equipe

- Grupos de **1 a 4 pessoas** (no máximo 4).
- **Todos os integrantes devem falar no vídeo.** Sugestão: dividam os testes entre vocês.
- **Todos os integrantes entregam o mesmo arquivo** no Classroom (nos checkpoints e na entrega final).

---

## 3. Cronograma

| Data | Etapa | Vale | O que entregar (Google Classroom) |
|---|---|---|---|
| **13/11 (hoje)** | **Checkpoint 1**: ambiente e primeiro contêiner | **Presença** | Um print com `http://localhost:5001/api/info` aberto **e** o `docker ps` mostrando o contêiner `cadastro-api` (o ID do contêiner tem que bater com o campo `maquina`), mais o arquivo `Dockerfile` da API do Cadastro. Manual: seções 1 a 4. |
| **27/11** | **Checkpoint 2**: tudo orquestrado | **Presença** | Os 6 `Dockerfile` + o `docker-compose.yml`, e um print do `docker compose ps` com **6 contêineres no ar** + a tela da Folha (porta 3003) com uma folha gerada. Manual: seções 5 e 6. |
| **04/12** | **Entrega final** | **Nota integral da N3** | Tudo o que está na seção 4 abaixo. |

> Não há aula em 20/11 (feriado da Consciência Negra). Use a semana para os Dockerfiles dos Sistemas 2 e 3.

---

## 4. Entrega final (04/12)

Um único arquivo **`N3_Sobrenome1_Sobrenome2_Sobrenome3_Sobrenome4.zip`** (um sobrenome por integrante) no Classroom contendo:

```
N3_Sobrenome1_Sobrenome2_Sobrenome3_Sobrenome4/
├── sistemas/
│   ├── docker-compose.yml          ← obrigatório
│   ├── cadastro/api/Dockerfile     ← + .dockerignore
│   ├── cadastro/web/Dockerfile     ← + .dockerignore
│   ├── ponto/api/Dockerfile        ← + .dockerignore
│   ├── ponto/web/Dockerfile        ← + .dockerignore
│   ├── folha/api/Dockerfile        ← + .dockerignore
│   └── folha/web/Dockerfile        ← + .dockerignore
├── README.md                       ← como executar + respostas das perguntas
├── slides.pdf                      ← apresentação
└── video.txt                       ← link do vídeo (YouTube não listado ou Google Drive com acesso liberado)
```

⚠️ **Não envie** as pastas `node_modules/`, `bin/`, `obj/`, `dist/` nem `dados/`.

⚠️ O professor vai testar assim: descompactar, abrir a pasta `sistemas/` e rodar **`docker compose up -d --build`**. **Tem que subir tudo sem nenhum passo extra.**

### 4.1 README.md (o que deve conter)

1. Nome completo de **todos** os integrantes (de 1 a 4).
2. Pré-requisitos e **comando para subir** o projeto.
3. Tabela com os **6 serviços**, as portas e o que cada um faz.
4. **Respostas curtas** (2 a 5 linhas cada):
   - a) Por que a tela usa `http://localhost:5001` e a `ponto-api` usa `http://cadastro-api:8080`?
   - b) Qual a diferença entre `docker pause` e `docker stop`, e o que o usuário percebe em cada caso?
   - c) Se a API do Cadastro cair, o que continua funcionando no Sistema 2? Por quê?
   - d) O que acontece com os dados se o contêiner for apagado **sem** volume? E **com** volume?
   - e) Por que usamos multi-stage build? Cite os tamanhos das imagens que vocês obtiveram.
5. Testes bônus realizados (se houver).

### 4.2 Vídeo (máximo de 10 minutos)

Grave a tela com áudio (OBS Studio, Xbox Game Bar `Win+G`, Loom, Teams...). **Vídeos com mais de 10 minutos perdem pontos.**

Roteiro sugerido:

| Tempo | Conteúdo |
|---|---|
| 0:00 – 1:00 | Apresentação do grupo e da arquitetura (3 sistemas, 6 contêineres, quem fala com quem) |
| 1:00 – 3:00 | **Como dockerizaram**: explicar um Dockerfile de API e um de front (multi-stage), o `docker-compose.yml` (portas, `environment`, `volumes`, `depends_on`, `restart`) |
| 3:00 – 4:00 | Subir do zero com `docker compose up -d --build` + **T1** (fluxo completo: cadastro → ponto → folha) |
| 4:00 – 9:00 | Testes **T2, T3, T4, T6, T7, T8 e T9** (obrigatórios), mostrando a tela, o terminal e os logs, e **explicando o porquê** de cada resultado |
| 9:00 – 10:00 | Conclusão: o que aprenderam, as dificuldades e o que mudariam numa arquitetura "de verdade" |

### 4.3 Slides (de 8 a 12 slides, em PDF)

1. Capa (disciplina, nomes, data)
2. O problema: "funciona na minha máquina" × contêineres
3. Arquitetura: um diagrama com os 6 contêineres, as portas, a rede e os arquivos JSON
4. Dockerfile da API (multi-stage), com destaque nas linhas principais
5. Dockerfile do front (Node → Nginx)
6. docker-compose.yml: serviços, rede pelo nome do serviço, volumes, restart
7. Tabela de testes: **teste → o que fizemos → o que aconteceu → por quê**
8. Resiliência: o que cai junto, o que continua (degradação parcial)
9. Persistência: com volume × sem volume; JSON corrompido
10. Conclusão e lições aprendidas

---

## 5. Critérios de avaliação (nota da N3)

| Critério | Peso |
|---|---|
| **Dockerfiles** corretos (multi-stage, `.dockerignore`, imagem final enxuta) | 2,0 |
| **docker-compose.yml** sobe tudo com um comando (portas, rede, variáveis, volumes, restart) | 2,0 |
| **Testes no vídeo**: T2, T3, T4, T6, T7, T8 e T9 demonstrados **e explicados** | 3,0 |
| **Explicação técnica** no vídeo (domínio do assunto; **todos os integrantes falam**) | 1,5 |
| **README** (respostas a–e) e **slides** | 1,5 |
| **Total** | **10,0** |
| Bônus (B1–B4 do manual) | até +1,0 |

**Descontos:**
- `docker compose up -d --build` não sobe na máquina do professor: **−3,0**
- Vídeo com mais de 10 minutos: **−1,0**
- Envio de `node_modules`/`bin`/`obj`: **−0,5**
- Trabalhos iguais entre equipes: **nota zero para as equipes envolvidas**

Os checkpoints valem **presença** nas aulas de 13/11 e 27/11.

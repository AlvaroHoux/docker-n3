# Projeto N3: Sistemas de RH (Cadastro, Ponto e Folha)

Comece por aqui:

1. **`ENUNCIADO.md`**: o que deve ser feito, as datas e o que entregar.
2. **`MANUAL-DOCKER.md`**: o passo a passo para criar os contêineres e os testes do vídeo.

## Estrutura

```
sistemas/
├── cadastro/api   → Sistema 1 - API .NET (porta 5001)   → dados/funcionarios.json
├── cadastro/web   → Sistema 1 - Tela React (porta 3001)
├── ponto/api      → Sistema 2 - API .NET (porta 5002)   → dados/ponto.json  (chama o Sistema 1)
├── ponto/web      → Sistema 2 - Tela React (porta 3002)
├── folha/api      → Sistema 3 - API .NET (porta 5003)   (chama os Sistemas 1 e 2)
└── folha/web      → Sistema 3 - Tela React (porta 3003)
```

## Variáveis de ambiente das APIs

| Variável | Usada por | Padrão (sem Docker) |
|---|---|---|
| `DATA_DIR` | cadastro-api, ponto-api | pasta `dados/` ao lado do executável |
| `CADASTRO_API_URL` | ponto-api, folha-api | `http://localhost:5001` |
| `PONTO_API_URL` | folha-api | `http://localhost:5002` |
| `VITE_API_URL` | telas React (no **build**) | ver o arquivo `.env` de cada tela |

## (Opcional) Rodar sem Docker

Só se você tiver o **.NET 10 SDK** e o **Node 20+** instalados. Abra **6 terminais**:

```bash
# terminais 1, 2 e 3 (APIs)
cd sistemas/cadastro/api && dotnet run
cd sistemas/ponto/api    && dotnet run
cd sistemas/folha/api    && dotnet run

# terminais 4, 5 e 6 (telas)
cd sistemas/cadastro/web && npm install && npm run dev
cd sistemas/ponto/web    && npm install && npm run dev
cd sistemas/folha/web    && npm install && npm run dev
```

Depois abra http://localhost:3001, http://localhost:3002 e http://localhost:3003.

> Repare que, sem Docker, você precisou instalar o SDK, o Node e abrir 6 terminais. Com Docker, será **um comando**.

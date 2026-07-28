---
status: pending
parallelizable: false
blocked_by: ["32.0", "41.0", "49.0", "50.0"]
---

<task_context>
<domain>inventory/documentation</domain>
<type>documentation</type>
<scope>configuration</scope>
<complexity>low</complexity>
<dependencies>http_server</dependencies>
<unblocks>""</unblocks>
<vertical_slice>Quem for operar ou investigar a F03 em produção encontra, em um só lugar, onde cada peça vive e o que fazer quando ela falha.</vertical_slice>
</task_context>

# Tarefa 51.0: Documentar o runbook e o README do controle de inventário

## Relacionada às User Stories

- [US-06] Gestor mede e decide sobre a exposição do piloto (suporte)
- [US-03] Bloquear datas imediatamente (suporte — o runbook é o que sustenta o SLA quando algo dá errado)

## Visão Geral

A F03 tem quatro peças que não são óbvias para quem não a implementou: a varredura de expiração, os gates de curadoria alimentados por configuração, os **dois calendários** que coexistem no módulo, e o rate limit que vive em outro repositório.

Cada uma delas produz uma investigação errada se não estiver documentada. O runbook existe para que a investigação comece no lugar certo.

## Requisitos

- Runbook cobrindo: varredura de expiração (intervalo, lote, backlog, o que fazer quando satura), replay de evento, gates de curadoria (origem, allowlist, como liberar uma propriedade), diagnóstico de saldo divergente e o procedimento de reconciliação.
- **Deixar explícito que os gates de curadoria vêm de configuração enquanto D06 não existe** — para que ninguém interprete configuração como decisão de D06.
- **Distinguir claramente as duas seções de calendário**: `Inventory:BusinessCalendar` (seg–sex 08h–18h, SLA da F01/F02) e `Inventory:InventoryServiceWindow` (seg–sáb 08h–20h, SLA da F03). Editar a errada muda silenciosamente um SLA já certificado.
- **Indicar que o rate limit do endpoint público vive em `localizestay-deploy`**, no router `lstay-api`, para que uma investigação de `429` em produção não comece procurando no backend.
- Listar os alertas e seus limiares, incluindo a latência de sessenta segundos do bloqueio emergencial.
- README atualizado com o contrato, as cinco permissões, as duas ondas e o estado de certificação.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/docs/runbooks/inventory-control.md`
- **Modificar:**
  - `../localizestay-backend/README.md` (contrato, permissões, ondas e certificação da F03)
- **Referência:**
  - `../localizestay-backend/docs/runbooks/commercial-offers.md` (padrão de runbook do projeto)
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/adrs/` (os cinco ADRs)
  - `../localizestay-backend/src/LocalizeStay.Api/appsettings.json` (as três seções de configuração da F03)
- **Skills para consultar durante implementação:**
  - `dotnet-observability` — alertas, limiares e o que cada métrica indica
  - `dotnet-production-readiness` — checklist de deploy

## Subtarefas

- [ ] 51.1 Escrever o runbook com as seções de varredura, replay de evento, gates de curadoria, saldo divergente e reconciliação.
- [ ] 51.2 Documentar as três seções de configuração da F03, com destaque para a distinção entre os dois calendários.
- [ ] 51.3 Documentar os alertas com seus limiares e apontar onde o rate limit do endpoint público realmente vive.
- [ ] 51.4 Atualizar o README com contrato, permissões, ondas e estado de certificação.

## Sequenciamento

- Bloqueado por: 32.0, 41.0, 49.0, 50.0
- Desbloqueia: Nenhuma — é a última tarefa do plano.
- Paralelizável: Não; documenta o comportamento final e depende da certificação concluída.

## Rastreabilidade

- Esta tarefa cobre: o passo 19 do Build Order da TechSpec e as exigências de documentação declaradas em ADR-002, ADR-003, ADR-004 e ADR-005.
- Evidência esperada: runbook publicado com as quatro armadilhas documentadas nominalmente.

## Detalhes de Implementação

Seções do runbook e o problema que cada uma previne:

| Seção | Problema que previne |
|---|---|
| Varredura de expiração | Investigar "retenção presa" olhando o handler quando o hosted service parou |
| Backlog de expiração saturando | Aumentar o lote sem entender que o gargalo é o lock de `daily_inventory` |
| Gates de curadoria por configuração | Concluir que D06 reprovou a propriedade quando ela só não está na allowlist |
| Dois calendários | Editar `Inventory:BusinessCalendar` querendo mudar o SLA da F03 e alterar silenciosamente o da F01/F02 |
| Rate limit na borda | Procurar em `RateLimitOptions` um `429` que vem do Traefik em outro repositório |
| Saldo divergente | Corrigir `daily_inventory` na mão em vez de rodar a reconciliação e achar a escrita que escapou do ledger |

Alertas a documentar:

| Alerta | Limiar | Ação |
|---|---|---|
| `inventory.block.emergency_latency` | **Qualquer** amostra > 60s | Viola a meta do PRD; investigar imediatamente |
| `inventory.hold.expiration_backlog` | Lote saturado em ciclos consecutivos | Verificar contenção de lock antes de aumentar o lote |
| Outbox sem processamento | Após o limite de retentativas | Verificar `inventory.outbox.failures` |
| `inventory.metrics.coverage_duration` | p95 > 2s por 7 dias | **Abre o ADR de projeção assíncrona** |
| `429` no router `lstay-api` | Taxa anormal | Limite mal calibrado ou abuso real — ajustar em `localizestay-deploy` |
| Divergência na reconciliação | Qualquer ocorrência | Uma escrita escapou do `InventoryLedger`; achar qual |

> A nota mais importante do runbook é a do `ipstrategy.depth`: com a cadeia Cloudflare → Traefik → app, `X-Forwarded-For` chega com mais de um endereço. Com `depth` errado, o Traefik particiona pelo IP da Cloudflare e recria o gargalo global — **agora mais difícil de diagnosticar, porque parece configurado.**

Configurações da F03 a documentar:

```
Inventory:InventoryServiceWindow   seg–sáb 08h–20h, America/Fortaleza, feriados (SLA da F03)
Inventory:HoldExpiration           intervalo 30s, lote 200
Inventory:CurationSellability      allowlist de propriedades aprovadas, default blocked
```

**Convenções da stack (das skills consultadas):**

- Runbook em `docs/runbooks/`, seguindo o formato de `commercial-offers.md`.
- Alertas com limiar numérico e ação, nunca "monitorar" genérico (`dotnet-observability`).
- Checklist de deploy conforme `dotnet-production-readiness`.

## Critérios de Sucesso (Verificáveis)

- [ ] O runbook existe em `docs/runbooks/inventory-control.md` com as seis seções da tabela.
- [ ] O runbook declara explicitamente que os gates de curadoria vêm de configuração enquanto D06 não existe.
- [ ] O runbook distingue nominalmente `Inventory:BusinessCalendar` de `Inventory:InventoryServiceWindow`, com o SLA que cada um sustenta.
- [ ] O runbook aponta `localizestay-deploy`, router `lstay-api`, como o lugar onde o rate limit do endpoint público vive, e alerta sobre `ipstrategy.depth`.
- [ ] Os seis alertas estão documentados com limiar numérico e ação.
- [ ] O README lista as cinco permissões `inventory:*`, as duas ondas e o estado de certificação.
- [ ] Build e suíte completos verdes: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore` e `dotnet test ../localizestay-backend/LocalizeStay.sln`

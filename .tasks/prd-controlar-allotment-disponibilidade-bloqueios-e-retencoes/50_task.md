---
status: pending
parallelizable: false
blocked_by: ["35.0", "43.0", "45.0", "46.0", "47.0", "48.0", "49.0"]
---

<task_context>
<domain>inventory/testing/end-to-end</domain>
<type>testing</type>
<scope>core_feature</scope>
<complexity>medium</complexity>
<dependencies>database</dependencies>
<unblocks>"51.0"</unblocks>
<vertical_slice>O fluxo real da F03 — allotment, bloqueio, retenção, comprometimento — é exercitado de ponta a ponta sobre uma propriedade estruturada pela F02.</vertical_slice>
</task_context>

# Tarefa 50.0: Certificar o fluxo ponta a ponta da F03

## Relacionada às User Stories

- [US-01], [US-02], [US-03], [US-04], [US-05], [US-06] (todas — é o teste que prova a feature inteira funcionando junta)

## Visão Geral

Teste de integração que percorre a jornada real: uma propriedade estruturada pela F02 recebe allotment, aparece na disponibilidade pública, sofre um bloqueio, é retida por um checkout, é comprometida por uma reserva confirmada, e tem tudo isso refletido no calendário, nas métricas e nos eventos.

É o teste que pega as falhas que nenhum teste de fatia pega: interações entre gates, saldo, bloqueios e retenções que só aparecem quando tudo está montado.

## Requisitos

- Partir de uma propriedade real estruturada pela F02, com acomodação, tarifa e política — não de um seed sintético que pule os gates.
- Percorrer: gates de vendabilidade → allotment → disponibilidade pública → solicitação na fila → bloqueio planejado → retenção → comprometimento → calendário → métricas.
- Verificar os seis eventos de domínio na outbox, com payload conforme o contrato.
- Verificar a trilha de auditoria em cada alteração de capacidade.
- Verificar a coerência do saldo em cada etapa: o que a consulta pública diz, o que o calendário mostra e o que `daily_inventory` guarda precisam concordar.
- Incluir o caminho de exceção: bloqueio emergencial sobre data com retenção e reserva, verificando invalidação, `bloqueio-afeta-reserva` e reserva intacta.
- Rodar contra PostgreSQL real via Testcontainers.

## Arquivos Envolvidos

- **Criar:**
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/InventoryEndToEndTests.cs`
- **Referência:**
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Inventory/CommercialOfferEndToEndTests.cs` (padrão de E2E da F02)
  - `../localizestay-backend/tests/LocalizeStay.IntegrationTests/Infrastructure/LocalizeStayWebApplicationFactory.cs`
  - `.tasks/prd-controlar-allotment-disponibilidade-bloqueios-e-retencoes/prd.md` (critérios de saída das duas ondas)
- **Skills para consultar durante implementação:**
  - `dotnet-testing` — Testcontainers PostgreSQL, cenário longo com asserções intermediárias
  - `restful-api` — exercitar pela API, não pelo DbContext
  - `dotnet-observability` — verificar eventos e auditoria

## Subtarefas

- [ ] 50.1 Montar o cenário base pela API da F02 e satisfazer os cinco gates de vendabilidade.
- [ ] 50.2 Percorrer o fluxo feliz: allotment → disponibilidade pública → solicitação na fila → bloqueio planejado → retenção → comprometimento, com asserções de saldo em cada etapa.
- [ ] 50.3 Percorrer o caminho de exceção: bloqueio emergencial sobre data com retenção e reserva, verificando invalidação, evento para D05 e reserva intacta.
- [ ] 50.4 Verificar, ao final, os seis eventos na outbox, a trilha de auditoria e a coerência entre consulta pública, calendário e métricas.

## Sequenciamento

- Bloqueado por: 35.0, 43.0, 45.0, 46.0, 47.0, 48.0, 49.0
- Desbloqueia: 51.0
- Paralelizável: Não; depende de toda a superfície montada e é o último portão antes da documentação.

## Rastreabilidade

- Esta tarefa cobre: os critérios de saída das duas ondas do PRD e o passo 18 do Build Order da TechSpec.
- Evidência esperada: `InventoryEndToEndTests` verde, com os seis eventos e a coerência de saldo em cada etapa.

## Detalhes de Implementação

Roteiro do cenário:

```
 1. F02: propriedade incorporada, acomodação, tarifa, política   ==> gates de D02 satisfeitos
 2. Allowlist de curadoria                                        ==> gates de D06 satisfeitos
 3. GET /sellability                                              ==> sellable: true, 5 gates satisfied
 4. POST /inventory-requests (whatsapp, allotmentGrant)           ==> 201, SLA derivado
 5. POST /allotments (3 unidades, 30 dias, requestId)             ==> 201, 30 linhas materializadas
 6. GET /availability                                             ==> bookable: true, availableUnits: 3
 7. PATCH /inventory-requests/{id} (processed, resultingAllotmentId) ==> processedWithinSla: true
 8. POST /inventory-blocks (planned, 1 unidade, 5 datas)          ==> 201, evento inventario-bloqueado
 9. GET /inventory-calendar                                       ==> composição correta nas 30 datas
10. POST /inventory-holds (1 unidade, 3 noites)                   ==> 201, evento inventario-retido
11. GET /availability                                             ==> availableUnits reduzido
12. POST /inventory-holds/{id}/commitment                         ==> 201, evento inventario-comprometido
13. GET /daily-inventory/{date}                                   ==> committed=1, held=0, total inalterado
14. POST /inventory-holds (nova retenção na mesma data)           ==> 201
15. POST /inventory-blocks (emergency, confirmEmergencyImpact)    ==> 201
    ==> retenção do passo 14 invalidated + inventario-liberado
    ==> reserva do passo 12 INTACTA + bloqueio-afeta-reserva
16. GET /inventory-metrics                                        ==> unbackedSales: 0, slaCompliance: 100%
```

> **O passo 15 é o coração da feature.** Ele prova simultaneamente RN-15 (emergencial é sempre aceito e corta vendas), RN-16 (nenhuma reserva é cancelada ou alterada) e a promessa do PRD ao viajante: a acomodação fica garantida durante o checkout — e quando não pode ficar, o checkout é encerrado explicitamente em vez de falhar no pagamento.

Todas as interações passam pela **API HTTP**, nunca pelo `InventoryDbContext` diretamente. Um E2E que escreve no banco para acelerar o setup deixa de exercitar exatamente a camada que ele existe para validar.

**Convenções da stack (das skills consultadas):**

- Testcontainers PostgreSQL com a suíte completa aplicada (`dotnet-testing`).
- Interações pela API, com JWT de teste carregando as permissões corretas (`restful-api`).
- Eventos verificados contra os schemas normativos do contrato.
- Auditoria verificada separadamente dos logs de diagnóstico (`dotnet-observability`).

## Critérios de Sucesso (Verificáveis)

- [ ] Testes passam: `dotnet test ../localizestay-backend/tests/LocalizeStay.IntegrationTests --filter "FullyQualifiedName~InventoryEndToEndTests"`
- [ ] Build compila sem erros: `dotnet build ../localizestay-backend/LocalizeStay.sln --no-restore`
- [ ] Os 16 passos do roteiro concluem com o saldo esperado em cada asserção.
- [ ] Os seis eventos de domínio aparecem na outbox com payload conforme o contrato.
- [ ] A reserva comprometida no passo 12 permanece intacta após o bloqueio emergencial do passo 15.
- [ ] `bloqueio-afeta-reserva` é produzido para D05.
- [ ] Consulta pública, calendário e `daily_inventory` concordam em cada etapa.
- [ ] `unbackedSales.count` é 0 ao final.
- [ ] A trilha de auditoria registra autor, horário e motivo de cada alteração de capacidade.
- [ ] A suíte completa segue verde: `dotnet test ../localizestay-backend/LocalizeStay.sln`

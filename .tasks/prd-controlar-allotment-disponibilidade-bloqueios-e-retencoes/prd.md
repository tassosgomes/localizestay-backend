# PRD — F03: Controlar Allotment, Disponibilidade, Bloqueios e Retenções

## Visão Geral

A funcionalidade estabelece o saldo vendável da LocalizeStay e o protege durante o checkout. Ela transforma o allotment contratado com o parceiro em capacidade diária consultável, permite que a Operação interna reduza essa capacidade quando a realidade operacional exigir e impede que duas jornadas concorrentes vendam a mesma unidade.

É o núcleo que sustenta a promessa de que uma reserva paga será reconhecida pelo parceiro. Sem ela, D01 exibe oferta sem lastro e D03 confirma compromissos que o hotel não pode honrar.

A entrega ocorre em duas ondas. A primeira estabelece o inventário e a consulta de disponibilidade, destravando descoberta. A segunda entrega o ciclo de retenção, validado ponta a ponta junto com `D03-C01`.

## Rastreabilidade

### Vision Doc

- **Objetivos atendidos:** reduzir overbooking e reserva não reconhecida; sustentar oito propriedades ativas em dois destinos; viabilizar o MVP transacional da Fase 2.
- **Restrições:** operação assistida, equipe enxuta, aplicação web responsiva, LGPD, prazo global de seis meses.
- **Non-Goals:** PMS, channel manager, integração hoteleira automática, autonomia do parceiro, automação total de exceções.

### Domain Doc

- **Feature:** `F03 — Controlar allotment, disponibilidade, bloqueios e retenções`.
- **Capacidade:** `D02-C03 — Controlar disponibilidade e bloqueios`.
- **Entidades:** `Allotment`, `Inventário Diário`, `Bloqueio`, `Retenção de Inventário`, `Acomodação`, `Propriedade`.
- **Regras:** `RN-01` a `RN-07`, `RN-14`, `RN-15` e `RN-16`.
- **Upstream:** `F01`, `F02`, `D06-C01` e `D08-C03`.
- **Downstream:** `D01-C01`, `D01-C02`, `D03-C01`, `D05-C03`, `D07-C01` e `D09-C03`.
- **Eventos consumidos:** `reserva.intencao-iniciada`, `reserva.confirmada`, `reserva.nao-concluida`, `curadoria-qualidade.propriedade-aprovada` e `curadoria-qualidade.propriedade-suspensa`.
- **Eventos produzidos:** `inventario-bloqueado`, `inventario-retido`, `retencao-expirada`, `inventario-liberado`, `inventario-comprometido` e `bloqueio-afeta-reserva`.

**Divergência consciente com `RN-08`:** allotment e bloqueio têm efeito imediato neste PRD, sem passar por `Alteração Pendente`. `RN-08` colide com `RN-15`, que exige interrupção imediata de vendas em emergência, e uma fila de aprovação no caminho crítico do inventário inviabilizaria o SLA de quatro horas. A governança de `RN-08` e `RN-09` será aplicada sobre estas operações pelo `F04`, sem alterar o modelo de dados aqui definido. A proteção no MVP é acesso restrito (`D08-C03`) e trilha de auditoria.

## Objetivos

- Garantir que nenhuma reserva seja confirmada sem saldo disponível na data.
- Garantir que nenhuma acomodação seja ofertada em D01 sem allotment vigente.
- Processar solicitações de allotment e bloqueio recebidas por WhatsApp ou e-mail em até quatro horas úteis.
- Interromper novas vendas em bloqueio emergencial em até um minuto após a confirmação da ação.
- Sustentar oito propriedades com allotment vigente cobrindo os noventa dias contínuos da janela do piloto.
- Atingir o piso comercial de duas unidades exclusivas por categoria vendável em cada propriedade do piloto.

## Histórias de Usuário

- Como operador, quero registrar o allotment contratado para que a acomodação passe a ter saldo vendável.
- Como operador, quero enxergar num calendário o que foi cedido, comprometido, retido, bloqueado e o que restou, para diagnosticar uma data sem alternar telas.
- Como operador, quero bloquear datas imediatamente ao receber um aviso de indisponibilidade, para não vender o que o parceiro não pode honrar.
- Como viajante, quero que a acomodação escolhida fique separada enquanto concluo o checkout, para não perder a unidade durante o pagamento.
- Como parceiro, quero solicitar allotment e bloqueios pelos canais que já uso.
- Como gestor, quero medir vendas sem lastro e prazo de processamento para decidir sobre a exposição do piloto.

## Funcionalidades Principais

### RF-01: Ceder allotment por acomodação e período

**Descrição:** Registrar a quantidade de unidades cedida exclusivamente à LocalizeStay para uma acomodação em um período contínuo, gerando `Inventário Diário` uniforme em todas as datas do período.

**Critérios de Aceitação:**

- **Given** uma acomodação com oferta estruturada em F02
  **When** o operador registra quantidade e período
  **Then** cada data do período passa a ter total cedido igual à quantidade informada.

- **Given** dois allotments da mesma acomodação
  **When** seus períodos se sobrepõem
  **Then** a operação é bloqueada até que os períodos sejam corrigidos.

- **Given** uma redução de allotment que deixaria o total cedido abaixo do comprometido em alguma data
  **When** o operador confirma a alteração
  **Then** a ação é bloqueada e o sistema indica registrar um bloqueio, pois allotment representa o contrato.

- **Given** um allotment com menos de duas unidades para a categoria
  **When** ele é registrado
  **Then** o cadastro é aceito, mas a categoria é sinalizada como abaixo do piso comercial do piloto e não conta para a meta de cobertura.

**Prioridade:** Must Have
**Rastreabilidade:** RN-01, RN-02 e RN-07.

### RF-02: Aplicar e remover bloqueios

**Descrição:** Reduzir ou zerar a capacidade vendável de uma acomodação em um período, com motivo obrigatório e distinção entre bloqueio planejado e emergencial.

**Critérios de Aceitação:**

- **Given** um bloqueio planejado
  **When** o operador o aplica
  **Then** ele consome apenas saldo livre, nunca alcançando retenções vigentes ou reservas confirmadas.

- **Given** um bloqueio planejado maior que o saldo livre da data
  **When** o operador confirma
  **Then** a ação é bloqueada e o saldo livre disponível é informado.

- **Given** um bloqueio emergencial
  **When** o operador o aplica
  **Then** ele é sempre aceito, as novas vendas cessam imediatamente e `inventario-bloqueado` é produzido.

- **Given** um bloqueio emergencial que alcança datas com reserva confirmada
  **When** ele é aplicado
  **Then** `bloqueio-afeta-reserva` é produzido para D05 como caso crítico e nenhuma reserva é cancelada ou alterada.

- **Given** um bloqueio removido
  **When** a remoção é confirmada
  **Then** a capacidade correspondente volta a ser vendável e o histórico do bloqueio é preservado.

**Prioridade:** Must Have
**Rastreabilidade:** RN-03, RN-15 e RN-16.

### RF-03: Calcular e consultar o saldo vendável

**Descrição:** Expor, para uma acomodação e um intervalo de datas, o saldo disponível igual ao total cedido menos reservas confirmadas, retenções vigentes e bloqueios aplicáveis.

**Critérios de Aceitação:**

- **Given** uma consulta para um período de estadia
  **When** o saldo é calculado
  **Then** a acomodação só é considerada disponível se todas as noites da estadia tiverem saldo suficiente para a quantidade solicitada.

- **Given** uma propriedade sem aprovação vigente, sem tarifa válida ou sem canal testado
  **When** a disponibilidade é consultada
  **Then** ela não é retornada como vendável, independentemente do saldo.

- **Given** uma data sem allotment cadastrado
  **When** a disponibilidade é consultada
  **Then** o saldo é zero, e não indefinido.

**Prioridade:** Must Have
**Rastreabilidade:** RN-02, RN-03 e RN-07.

### RF-04: Operar o calendário de inventário

**Descrição:** Apresentar, por propriedade e acomodação, uma grade de datas com total cedido, comprometido, retido, bloqueado e disponível, permitindo ceder allotment e aplicar ou remover bloqueios a partir da própria grade.

**Critérios de Aceitação:**

- **Given** uma data sem saldo disponível
  **When** o operador a inspeciona no calendário
  **Then** enxerga qual parcela é comprometida, retida ou bloqueada e o motivo de cada bloqueio.

- **Given** uma solicitação recebida por WhatsApp ou e-mail
  **When** o operador registra a alteração
  **Then** origem, canal, responsável e horário de recebimento ficam registrados para apuração do SLA de quatro horas úteis.

- **Given** uma solicitação recebida fora da janela de atendimento
  **When** o SLA é apurado
  **Then** a contagem começa às 08h00 do próximo período útil.

- **Given** um aviso de bloqueio emergencial recebido fora da janela
  **When** a janela abre
  **Then** ele aparece no topo da fila, antes de qualquer solicitação de allotment, e o horário original de recebimento fica registrado.

**Prioridade:** Must Have
**Rastreabilidade:** RN-01 e RN-14.

### RF-05: Interromper vendas por decisão de curadoria

**Descrição:** Cessar a venda de toda a propriedade quando D06 comunicar suspensão, e restabelecê-la quando a aprovação voltar a vigorar.

**Critérios de Aceitação:**

- **Given** o evento `curadoria-qualidade.propriedade-suspensa`
  **When** ele é recebido
  **Then** todas as acomodações da propriedade deixam de ser vendáveis com efeito equivalente a um bloqueio emergencial de origem D06, sem cancelar reservas confirmadas.

- **Given** uma propriedade suspensa com reservas confirmadas no período
  **When** a suspensão é aplicada
  **Then** `bloqueio-afeta-reserva` é produzido para D05.

**Prioridade:** Must Have
**Rastreabilidade:** RN-07, RN-15 e RN-16.

### RF-06: Reter inventário no início do checkout

**Descrição:** Separar temporariamente a capacidade solicitada quando D03 informa uma intenção de reserva, antes de qualquer tentativa de pagamento.

**Critérios de Aceitação:**

- **Given** uma intenção de reserva com saldo suficiente em todas as noites
  **When** `reserva.intencao-iniciada` é recebido
  **Then** a retenção é criada com prazo de expiração e `inventario-retido` é produzido.

- **Given** uma intenção de reserva sem saldo suficiente em ao menos uma noite
  **When** a retenção é solicitada
  **Then** nenhuma capacidade é separada e D03 recebe a recusa com a data indisponível.

- **Given** duas intenções concorrentes para a última unidade da data
  **When** ambas solicitam retenção
  **Then** apenas uma é criada e a outra é recusada.

- **Given** um bloqueio emergencial sobre datas com retenções vigentes
  **When** ele é aplicado
  **Then** as retenções são invalidadas, `inventario-liberado` é produzido e D03 é informado para encerrar o checkout.

**Prioridade:** Must Have
**Rastreabilidade:** RN-04, RN-05 e RN-15.

### RF-07: Expirar e liberar retenções

**Descrição:** Devolver ao saldo vendável a capacidade retida quando o prazo termina ou quando a jornada encerra sem reserva confirmada.

**Critérios de Aceitação:**

- **Given** uma retenção vigente
  **When** seu prazo termina sem confirmação
  **Then** a capacidade volta a ser vendável e `retencao-expirada` e `inventario-liberado` são produzidos.

- **Given** o evento `reserva.nao-concluida`
  **When** ele é recebido
  **Then** a retenção correspondente é liberada mesmo antes do prazo.

- **Given** uma retenção já expirada
  **When** `reserva.nao-concluida` chega para ela
  **Then** nenhuma capacidade é devolvida duas vezes.

**Prioridade:** Must Have
**Rastreabilidade:** RN-04 e RN-05.

### RF-08: Comprometer inventário na confirmação

**Descrição:** Converter a retenção em capacidade comprometida quando a reserva é confirmada, sem novo consumo do saldo.

**Critérios de Aceitação:**

- **Given** uma retenção vigente
  **When** `reserva.confirmada` é recebido
  **Then** a capacidade migra de retida para comprometida sem alterar o total disponível e `inventario-comprometido` é produzido.

- **Given** uma retenção já expirada
  **When** `reserva.confirmada` é recebido
  **Then** a confirmação só é aceita se ainda houver saldo disponível; caso contrário, a divergência é comunicada a D03 e D07 sem comprometer capacidade inexistente.

**Prioridade:** Must Have
**Rastreabilidade:** RN-05 e RN-06.

## Experiência do Usuário

A Operação abre uma propriedade já estruturada em F02 e registra o allotment contratado por acomodação e período. A partir daí, o calendário de inventário passa a ser a tela de trabalho: cada data mostra a composição do saldo e aceita ações diretas.

Ao receber um aviso do parceiro, o operador aplica um bloqueio escolhendo entre planejado e emergencial. O emergencial exige confirmação explícita e apresenta, antes de concluir, quantas reservas confirmadas e retenções serão afetadas.

Estados e bloqueios devem ser comunicados sem depender apenas de cores, com navegação por teclado e mensagens que indiquem a ação corretiva. O viajante não interage com esta funcionalidade: percebe apenas que a acomodação permanece garantida durante o checkout e recebe um aviso claro quando a retenção expira.

## Restrições Técnicas de Alto Nível

- Acesso restrito à equipe interna autorizada, com trilha de auditoria de autor, horário e motivo em toda alteração de capacidade.
- A duração da retenção é um parâmetro global da plataforma, mantido fixo em quinze minutos durante as Ondas A e B para não introduzir variação no ponto de maior concorrência do sistema.
- A janela de atendimento das solicitações de allotment e bloqueio é de segunda a sábado, das 08h00 às 20h00. Fora dela, a contagem do SLA de quatro horas começa às 08h00 do próximo período útil.
- Bloqueio emergencial tem prioridade máxima na fila dentro da janela de atendimento. Não há plantão fora dela: avisos recebidos de madrugada, aos domingos ou em feriados são a primeira ação da abertura do próximo período.
- Uma vez confirmado no painel, o bloqueio emergencial cessa novas vendas em até um minuto. Esse prazo mede o sistema, não a disponibilidade humana.
- Aplicação web responsiva e acessível.
- Parceiros continuam usando WhatsApp ou e-mail; nenhum canal altera o inventário automaticamente.
- Quantidades são expressas em unidades inteiras de acomodação.

## Não-Objetivos

- Aprovação de alterações por `Alteração Pendente`, que pertence ao F04.
- Verificação da propriedade, conteúdo publicável e decisão de suspensão, que pertencem a D06.
- Criação, confirmação, cancelamento ou consulta de reserva, que pertencem a D03.
- Cobrança, estorno, comissão e repasse, que pertencem a D04.
- Mediação, realocação e tratamento humano de overbooking, que pertencem a D05.
- Busca, filtros, ordenação e página pública da hospedagem, que pertencem a D01.
- Allotment livre, garantido sob consulta, release period ou stop-sell por regra tarifária.
- Overbooking intencional, edição do inventário pelo parceiro, PMS, channel manager e sincronização automática.
- Preço dinâmico ou qualquer decisão comercial derivada da ocupação.

## Plano de Rollout Faseado

### MVP — Onda A (Inventário)

- **Funcionalidades:** RF-01 a RF-05.
- **Critério para avançar à Onda B:** oito propriedades com allotment vigente na janela do piloto e nenhuma acomodação exposta a D01 sem allotment vigente.

### MVP — Onda B (Retenção)

- **Funcionalidades:** RF-06 a RF-08, validadas ponta a ponta com `D03-C01`.
- **Critério para avançar ao piloto:** criação, expiração, liberação e comprometimento testados de ponta a ponta, sem venda sem lastro em teste de concorrência.

### Phase 2

- Painel de retenções vigentes e alerta de esgotamento próximo, quando houver volume que justifique.
- Calibração da duração da retenção a partir de dados reais de conversão e expiração medidos por D09, mantendo o parâmetro único e desacoplado do meio de pagamento.
- Governança de alterações via F04 sobre allotment e bloqueios.

### Phase 3

- Participação direta do parceiro no inventário, somente por meio do F05.
- Expansão do portfólio condicionada às evidências de D06, D07 e D09.

## Métricas de Sucesso

| Métrica | Definição | Meta | Prazo |
|---|---|---:|---|
| Venda sem lastro | Reservas confirmadas sem saldo disponível na data | 0 | Desde a Onda A |
| Oferta sem allotment | Acomodações expostas a D01 sem allotment vigente | 0 | Desde a Onda A |
| SLA de processamento | Solicitações tratadas em até 4h dentro da janela de segunda a sábado, 08h–20h | 100% | Desde a Onda A |
| Cobertura de inventário | Propriedades com allotment vigente nos 90 dias do piloto e ao menos duas unidades por categoria | ≥ 8 | Antes do piloto |
| Latência do bloqueio emergencial | Tempo entre a confirmação no painel e o corte de novas vendas | ≤ 1 min | Desde a Onda A |
| Exposição fora da janela | Reservas confirmadas em datas alvo de aviso emergencial pendente de abertura da janela | Monitorada | Desde a Onda A |
| Expiração de retenção | Retenções que expiram sem reserva confirmada | Monitorada | Desde a Onda B |

## Riscos e Mitigações

- **Parceiro vender externamente a capacidade cedida:** exclusividade contratual com consequência escalonada — na primeira ocorrência que gerar overbooking e realocação por D05, o parceiro arca com o valor integral de uma diária de contingência; na reincidência, advertência formal e suspensão temporária da vitrine por D06. Complementada por acompanhamento dos primeiros check-ins e bloqueio emergencial disponível a qualquer momento.
- **Solicitação ultrapassar o SLA:** registro de origem e horário no calendário, prioridade para bloqueios e medição contínua do prazo dentro da janela acordada.
- **Retenção mal calibrada prejudicar conversão ou disponibilidade:** parâmetro congelado em quinze minutos durante o MVP e monitoramento da taxa de expiração para embasar a calibração na Phase 2.
- **Erro operacional sem camada de aprovação até o F04:** acesso restrito, trilha de auditoria, bloqueio de reduções de allotment abaixo do comprometido e confirmação explícita no bloqueio emergencial.
- **Bloqueio emergencial atingir reserva confirmada:** cessar novas vendas e acionar D05 imediatamente, sem cancelamento automático.
- **Venda indevida entre o aviso emergencial e a abertura da janela:** sem plantão noturno, um aviso de madrugada só vira bloqueio às 08h00, e `RN-15` fica cumprido apenas dentro da janela. Mitigação: registrar o horário original do aviso, priorizá-lo na abertura, monitorar a exposição do intervalo e tratar por D05 qualquer reserva confirmada nele. Se a métrica indicar reincidência, reabrir a decisão sobre plantão.
- **Onda A entrar em produção sem retenção:** não expor checkout a viajantes reais antes da Onda B.

## Alternativas Consideradas

### Abordagem escolhida: Duas ondas, inventário antes de retenção

Entrega allotment, bloqueios e consulta de disponibilidade primeiro, destravando `D01-C01` sem esperar por Reserva, e depois o ciclo de retenção co-validado com `D03-C01`. Foi escolhida por alinhar-se às Ondas 2 e 3 do Capability Backlog e gerar valor antes do núcleo transacional estar pronto.

### Alternativa rejeitada: Incremento único ponta a ponta

Fecharia F03 apenas com a retenção pronta e validada com D03. Faria `RN-03` nascer completo e evitaria retrabalho no cálculo de saldo, mas bloquearia toda a Onda 2 — busca e página de hospedagem ficariam esperando Reserva.

### Alternativa rejeitada: Retenção primeiro

Atacaria antes o risco de checkout concorrente, com allotment cadastrado manualmente. Provaria o núcleo crítico cedo, mas entregaria um inventário que a Operação não consegue manter dentro do SLA e que não sustenta descoberta.

### Alternativa rejeitada: Allotment editável por data

Permitiria definir saldo dia a dia em calendário. Daria flexibilidade próxima a um channel manager, mas diluiria a noção contratual exigida por `RN-02`, tornaria `Bloqueio` redundante e ampliaria a carga manual de uma equipe enxuta.

## Decisões Comerciais e Operacionais Confirmadas

| Tema | Decisão |
|---|---|
| Piso de allotment | Duas unidades exclusivas por categoria vendável. Categoria com uma unidade é comercializável, mas sinalizada e fora da meta de cobertura |
| Janela de atendimento | Segunda a sábado, 08h00 às 20h00; fora dela o SLA de 4h conta a partir das 08h00 do próximo período útil |
| Bloqueio emergencial | Sem plantão fora da janela. Prioridade máxima na fila e primeira ação da abertura; corte de vendas em até um minuto após a confirmação no painel |
| Venda externa da capacidade | Primeira ocorrência com overbooking: diária de contingência custeada pelo parceiro. Reincidência: advertência formal e suspensão temporária da vitrine por `D06-C03`, única alavanca de curadoria disponível no MVP |
| Janela do piloto | Dois destinos correlatos no Nordeste, em janela contínua de noventa dias cobrindo média e alta temporada |
| Duração da retenção | Parâmetro único fixo em quinze minutos, desacoplado do meio de pagamento; recalibração apenas na Phase 2, com evidência de D09 |

## Questões em Aberto

- Definir o par exato de destinos do corredor turístico e a data de início dos noventa dias — responsável: Ramon; prazo: antes da Onda A. Sem isso, a meta de cobertura não tem período de referência.
- Confirmar a escala que sustenta a janela de segunda a sábado, 08h00 às 20h00 — responsável: Operação interna com D07; prazo: antes da Onda A. Se a escala não cobrir a janela inteira, o SLA de quatro horas precisa ser recalculado antes de constar do contrato do parceiro.
- Validar com Jurídico a redação da cláusula de exclusividade e da diária de contingência — responsável: Jurídico com Ramon; prazo: antes do piloto com dinheiro real.
- Comunicar formalmente aos parceiros a janela de atendimento e a ausência de plantão noturno — responsável: Ana; prazo: na contratação do primeiro lote. Sem isso, o parceiro pode presumir cobertura contínua ao avisar uma emergência de madrugada.

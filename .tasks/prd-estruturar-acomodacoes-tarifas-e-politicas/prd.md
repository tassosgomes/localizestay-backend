# PRD — F02: Estruturar Acomodações, Tarifas e Políticas

## Visão Geral

A funcionalidade permite que a Operação interna transforme informações comerciais recebidas dos parceiros em ofertas consistentes, comparáveis e prontas para revisão.

A oferta será estruturada progressivamente. Uma propriedade poderá avançar quando possuir pelo menos uma acomodação completa, com ocupação, tarifa vigente, política e regime de alimentação definidos. Um segundo operador deverá validar os dados comerciais antes do envio.

## Rastreabilidade

### Vision Doc

- **Objetivos atendidos:** organizar e qualificar a oferta regional; oferecer clareza sobre acomodações, preços e políticas; preparar pelo menos oito propriedades ativas.
- **Restrições:** operação assistida, equipe enxuta, aplicação web responsiva, LGPD, relação de consumo e prazo global de seis meses.
- **Non-Goals:** PMS, channel manager, CRM hoteleiro, aplicativo nativo, autonomia avançada do parceiro e automação total.

### Domain Doc

- **Feature:** `F02 — Estruturar acomodações, tarifas e políticas`.
- **Capacidade:** `D02-C02 — Estruturar a oferta comercial da propriedade`.
- **Entidades:** `Propriedade`, `Acomodação`, `Tarifa Comercial` e `Política da Propriedade`.
- **Regras:** `RN-01`, `RN-07`, `RN-10`, `RN-11`, `RN-12`, `RN-13` e `RN-14`.
- **Upstream:** `D02-C01 — Incorporar parceiros e propriedades`.
- **Downstream:** D06-C02, D02-C03, D01-C01, D01-C02 e D03-C01.
- **Eventos consumidos:** nenhum.
- **Evento produzido:** `oferta-inventario.oferta-estruturada`.

D06-C01 pode evoluir paralelamente, mas verificação e aprovação permanecem fora deste PRD.

## Objetivos

- Estruturar ofertas sem ambiguidade comercial.
- Obter pelo menos 90% de aceitação na primeira revisão, sem devolução por dados ausentes ou inconsistentes.
- Enviar a oferta à primeira revisão em até dois dias úteis após o recebimento de todas as informações.
- Garantir dupla validação em 100% das ofertas enviadas.
- Preparar pelo menos oito propriedades com uma acomodação completa antes do piloto.
- Processar solicitações recebidas por WhatsApp ou e-mail em até quatro horas úteis.

## Histórias de Usuário

- Como operador, quero cadastrar acomodações e condições comerciais progressivamente para tratar informações incompletas sem perder o trabalho realizado.
- Como operador, quero reutilizar políticas cadastradas e definir uma política padrão por propriedade para reduzir inconsistências.
- Como revisor, quero conferir preços, ocupação e políticas antes do envio para evitar ofertas incorretas.
- Como parceiro, quero fornecer condições comerciais pelos canais já utilizados.
- Como gestor, quero medir prazo, completude e retrabalho para avaliar a capacidade operacional.

## Funcionalidades Principais

### RF-01: Manter políticas da propriedade

**Descrição:** Permitir o cadastro, associação e definição de uma política padrão por propriedade, usando somente os tipos Flexível e Não-Reembolsável e preservando suas regras aprovadas.

**Critérios de Aceitação:**

- **Given** uma propriedade sem política padrão  
  **When** o operador cadastra uma política válida  
  **Then** pode defini-la como padrão para novas acomodações.

- **Given** uma mudança da política padrão  
  **When** o operador confirma a alteração  
  **Then** escolhe se as acomodações existentes serão atualizadas.

- **Given** uma política associada a acomodações  
  **When** o operador solicita sua desativação  
  **Then** deve selecionar uma substituta antes de concluir.

**Prioridade:** Must Have  
**Rastreabilidade:** RN-11, RN-12 e RN-13.

### RF-02: Estruturar acomodações e ocupação

**Descrição:** Registrar nome comercial, categoria, configuração de camas, características estruturais, capacidade total, limites de adultos e crianças e faixa etária infantil.

**Critérios de Aceitação:**

- **Given** uma nova acomodação  
  **When** o operador registra seus dados  
  **Then** capacidade, adultos, crianças e configuração de camas devem ser coerentes.

- **Given** uma propriedade com faixa etária infantil padrão  
  **When** uma acomodação é criada  
  **Then** ela herda essa definição, permitindo substituição específica.

- **Given** ausência de descrição, fotos ou comodidades editoriais  
  **When** a acomodação comercial está completa  
  **Then** ela pode avançar na F02, pois esses conteúdos pertencem a D06.

**Prioridade:** Must Have  
**Rastreabilidade:** RN-01 e RN-07.

### RF-03: Definir tarifas comerciais

**Descrição:** Registrar tarifas em BRL com valor-base por diária, hóspedes incluídos, acréscimos distintos para adulto e criança, período, mínimo de noites, política e regime de alimentação.

Taxas obrigatórias devem estar incluídas no preço. Os regimes admitidos são sem alimentação, café da manhã, meia pensão e pensão completa.

**Critérios de Aceitação:**

- **Given** uma tarifa válida  
  **When** o operador a registra  
  **Then** informa valor-base, ocupação incluída, adicionais, período, mínimo de noites, política e alimentação.

- **Given** tarifas da mesma acomodação, política e condição comercial  
  **When** seus períodos se sobrepõem  
  **Then** a conclusão é bloqueada até que os períodos sejam corrigidos.

- **Given** uma estadia atravessando períodos tarifários  
  **When** seu preço é calculado  
  **Then** cada diária usa o valor de sua data e o mínimo de noites aplicável é o da data de check-in.

**Prioridade:** Must Have  
**Rastreabilidade:** RN-07, RN-10, RN-11, RN-12 e RN-13.

### RF-04: Gerenciar rascunhos e pendências

**Descrição:** Permitir salvamento progressivo e identificação dos dados que impedem o envio para revisão.

**Critérios de Aceitação:**

- **Given** informações incompletas  
  **When** o operador salva o cadastro  
  **Then** a oferta permanece em rascunho e apresenta suas pendências.

- **Given** um item nunca enviado para revisão  
  **When** o operador confirma sua exclusão  
  **Then** o item é removido.

- **Given** um item já enviado  
  **When** ele deixa de ser oferecido  
  **Then** é desativado com motivo e histórico preservado.

**Prioridade:** Must Have  
**Rastreabilidade:** RN-01 e RN-14.

### RF-05: Validar e enviar a oferta

**Descrição:** Submeter preços, ocupação e políticas à conferência de um segundo operador antes do envio.

**Critérios de Aceitação:**

- **Given** uma propriedade com pelo menos uma acomodação completa e um período tarifário atual ou futuro  
  **When** outro operador valida seus dados  
  **Then** a oferta pode receber o estado “pronta para revisão”.

- **Given** o mesmo operador que cadastrou a oferta  
  **When** tenta realizar a validação final  
  **Then** a ação é bloqueada.

- **Given** uma oferta validada  
  **When** preço, ocupação, política ou período é alterado  
  **Then** a validação é invalidada e uma nova conferência é exigida.

- **Given** os requisitos concluídos  
  **When** o envio é confirmado  
  **Then** o evento `oferta-inventario.oferta-estruturada` é produzido.

**Prioridade:** Must Have  
**Rastreabilidade:** RN-01 e RN-07.

### RF-06: Corrigir ofertas devolvidas

**Descrição:** Permitir correção e reenvio enquanto a oferta não estiver publicada.

**Critérios de Aceitação:**

- **Given** uma oferta devolvida com pendências  
  **When** o operador realiza as correções  
  **Then** o histórico é preservado e uma nova validação é exigida.

- **Given** uma oferta já publicada  
  **When** uma alteração é solicitada  
  **Then** ela não é processada pela F02 e deve seguir a governança de F04.

**Prioridade:** Must Have  
**Rastreabilidade:** RN-08, RN-09 e RN-10.

## Experiência do Usuário

A Operação acessa uma propriedade incorporada, cadastra suas políticas e define uma delas como padrão. Em seguida, estrutura acomodações e tarifas, podendo salvar rascunhos e acompanhar pendências.

Quando existir ao menos uma acomodação completa, outro operador revisa o resumo comercial. Alterações posteriores invalidam a validação. Estados, bloqueios e pendências devem ser comunicados sem depender apenas de cores, com navegação por teclado e mensagens acionáveis.

## Restrições Técnicas de Alto Nível

- Acesso restrito à equipe interna autorizada.
- Aplicação web responsiva e acessível.
- Parceiros continuam usando WhatsApp ou e-mail.
- Somente o operador que realizou o cadastro precisa ser registrado como evidência da origem interna.
- Dados pessoais devem respeitar LGPD.
- Valores são registrados exclusivamente em BRL.

## Não-Objetivos

- Allotment, inventário, disponibilidade, bloqueios ou retenções.
- Verificação da propriedade, fotos, descrições, comodidades e aprovação editorial.
- Publicação, busca ou página pública da hospedagem.
- Reserva, pagamento, comissão ou repasse.
- Alterações de ofertas já publicadas.
- Portal ou edição direta pelo parceiro.
- Políticas personalizadas além de Flexível e Não-Reembolsável.
- Promoções, cupons, preços dinâmicos ou múltiplas moedas.
- PMS, channel manager ou importação automática.

## Plano de Rollout Faseado

### MVP

- RF-01 a RF-06.
- Aplicação inicial ao lote de pelo menos oito propriedades.
- Avanço condicionado às metas de completude, prazo e dupla validação.

### Phase 2

- Avaliar melhorias de produtividade a partir das causas reais de atraso e retrabalho.
- Evoluir participação do parceiro somente por meio de F05.

### Phase 3

- Avaliar automação e expansão regional após evidências de confiabilidade, qualidade e capacidade operacional.

## Métricas de Sucesso

| Métrica | Meta | Prazo |
|---|---:|---|
| Ofertas aceitas na primeira revisão | ≥ 90% | Primeiro lote |
| Envio após informações completas | ≤ 2 dias úteis | Desde o MVP |
| Ofertas enviadas com dupla validação | 100% | Desde o MVP |
| Solicitações processadas no SLA | 100% em até 4 horas úteis | Desde o MVP |
| Propriedades com acomodação completa | ≥ 8 | Antes do piloto |

## Riscos e Mitigações

- **Preço incorreto:** dupla validação e invalidação após alterações.
- **Condições ambíguas:** campos obrigatórios, políticas padronizadas e bloqueio de sobreposição.
- **Complexidade excessiva:** apenas BRL, dois tipos de política e quatro regimes de alimentação.
- **Gargalo de revisão:** medir o prazo e distribuir revisores autorizados.
- **Dados desatualizados:** limitar o evento a períodos atuais ou futuros e tratar mudanças publicadas em F04.
- **Divergência com Curadoria:** preservar fronteiras e enviar somente dados comerciais.

## Alternativas Consideradas

### Abordagem escolhida: Oferta mínima vendável por acomodação

Entrega o fluxo comercial completo e permite avançar com uma acomodação válida. Foi escolhida por gerar valor cedo sem fragmentar o resultado de negócio.

### Alternativa rejeitada: Entrega em duas etapas

Separaria políticas e acomodações das tarifas. Reduziria o primeiro incremento, mas não produziria uma oferta utilizável por domínios downstream.

### Alternativa rejeitada: Lote completo por propriedade

Exigiria todas as acomodações antes do envio. Aumentaria a completude inicial, mas prolongaria o ciclo e concentraria retrabalho.

## Questões em Aberto

- Validar com Jurídico a redação final das políticas Flexível e Não-Reembolsável antes de operar dinheiro real.
- Definir os papéis e alçadas internas autorizados a realizar a segunda validação.
- Confirmar os destinos e a janela operacional do piloto antes da ativação comercial.

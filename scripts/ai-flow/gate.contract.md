# Contrato do Gate Determinístico do AI Flow

Este arquivo é copiado para `scripts/ai-flow/` do repositório alvo. Ele define a
interface que `ai-flow-validator`, `ai-flow-implementer` e `ai-flow-orchestrator`
dependem. **A implementação varia por linguagem; este contrato não.**

## Invocação

```bash
scripts/ai-flow/gate.sh [--filter=<expr>]... [--skip-tests] [--help]
```

- `--filter=<expr>` — expressão de seleção de testes, extraída dos critérios de
  sucesso verificáveis da task. Repetível. A sintaxe é da stack (ex.: `.NET`
  usa `FullyQualifiedName~X`, jest usa um regex de nome, pytest usa `-k`).
- `--skip-tests` — roda apenas format e build. Uso restrito a diagnóstico.

Flags opcionais adicionais específicas da stack são permitidas (ex.: `--sln=<path>`
no .NET), desde que o gate funcione sem elas. Nenhum consumidor do contrato pode
depender de uma flag específica de stack.

## Saída

Exatamente um de dois blocos, em stdout.

**Aprovado:**

```text
GATE: APROVADO
arquivos alterados: <n> (<ext>: <n>)
format: <status>
build: <status>
testes: <status>
```

**Reprovado:**

```text
GATE: REPROVADO
etapa: format|build|testes|diff-check
comando: <comando exato que falhou>
--- output (ultimas N linhas) ---
<saida truncada>
```

## Códigos de saída

| Código | Significado |
|---|---|
| `0` | APROVADO |
| `1` | REPROVADO — uma etapa falhou |
| `2` | Erro de uso ou de ambiente (argumento inválido, fora de repo git, stack não detectada) |

Exit `2` é distinto de `1` de propósito: o validator trata `1` como reprovação da
task e `2` como falha de infraestrutura, caindo para execução manual dos comandos.

## Invariantes obrigatórios

Qualquer implementação DEVE garantir os quatro itens abaixo. Eles não são
detalhes — são a razão de o gate existir.

1. **Escopo no diff.** Format e lint rodam apenas sobre os arquivos alterados desde
   o último commit (`git diff --name-only HEAD` + untracked). Rodar sobre o projeto
   inteiro faz o gate reprovar a task por débito pré-existente que ela não causou.

2. **Detecção de filtro vazio.** Se um `--filter` declarado pela task seleciona zero
   testes, o gate REPROVA. Sem isso, uma task que promete uma suíte e não a entrega
   passa no gate. É a falha mais comum e mais cara de detectar semanticamente.

3. **Saída truncada.** No máximo ~40 linhas do output do comando que falhou, e nada
   do output dos que passaram. O gate existe para não gastar contexto de LLM.

4. **Zero interatividade.** Nenhum prompt, nenhum pager, nenhum watch mode. Testes
   que exigem serviços externos (Testcontainers, Docker) devem falhar com mensagem
   clara em vez de pendurar.

## Consumidores

- `ai-flow-implementer` roda o gate antes de devolver a task ao orquestrador.
- `ai-flow-validator` roda o gate como estágio 1; se reprovar, rejeita sem abrir
  PRD, techspec ou skills.

Mudar este contrato exige atualizar as três skills de fluxo. Mudar a implementação
para outra linguagem, não.

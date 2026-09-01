using SMSMais.Core.Integracoes.SisregWeb.Mapeamento.Dtos;
using SMSMais.Data.Entities;

namespace SMSMais.Core.Integracoes.SisregWeb.Mapeamento;

/// <summary>
/// Mapeamento da unidade selecionada: profissionais do SISREG e seus procedimentos, com
/// habilita/desabilita para controlar o que entra na varredura de agenda.
///
/// <para>Todos os métodos exigem UMA unidade selecionada (header X-Unidade-Id).</para>
/// </summary>
public interface ISisregMapeamentoService
{
    /// <summary>Mapeamento persistido da unidade (não vai ao SISREG).</summary>
    Task<SisregMapeamentoDto> ObterAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconcilia o mapeamento de uma unidade <b>explícita</b> (sem depender do header
    /// <c>X-Unidade-Id</c>) e com autor explícito — a porta usada pelo motor em lote fora de uma
    /// request. <paramref name="antesDeCadaRequisicao"/> é chamado antes de cada ida ao SISREG
    /// (o lote usa para respeitar o teto de requisições/hora); passe <c>null</c> no uso interativo.
    /// </summary>
    /// <param name="ttlProcedimentos">
    /// Idade a partir da qual os procedimentos de um profissional já conhecido são rebuscados.
    /// <c>null</c> (uso interativo) busca sempre — é o "atualizar de verdade" que o operador
    /// espera do botão. O lote passa um TTL porque os procedimentos custam 1 requisição por
    /// profissional e são a maior parte do gasto da rede inteira.
    /// </param>
    Task<SisregMapeamentoAtualizacaoDto> AtualizarNoContextoAsync(
        Unidade unidade, Guid? usuarioId, Func<CancellationToken, Task>? antesDeCadaRequisicao,
        CancellationToken cancellationToken = default, TimeSpan? ttlProcedimentos = null);

    Task AlternarProfissionalAsync(Guid profissionalId, bool habilitado, CancellationToken cancellationToken = default);

    Task AlternarProcedimentoAsync(Guid procedimentoId, bool habilitado, CancellationToken cancellationToken = default);

    /// <summary>
    /// Liga/desliga o aviso por WhatsApp ao paciente quando este procedimento é importado NESTA
    /// unidade. Aplica a todas as linhas do mesmo procedimento na unidade (ele costuma aparecer
    /// sob vários profissionais) e devolve quantas foram afetadas.
    /// </summary>
    Task<int> AlternarEnvioConfirmacaoAsync(Guid procedimentoId, bool enviar, CancellationToken cancellationToken = default);

    /// <summary>Liga/desliga vários profissionais de uma vez.</summary>
    Task AlternarProfissionaisEmLoteAsync(IReadOnlyList<Guid> ids, bool habilitado, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aplica de uma vez a <b>toda a unidade</b>: todos os profissionais e todos os procedimentos
    /// deles, mais o aviso por WhatsApp. É o mesmo que o botão do médico faz, só que na unidade
    /// inteira — existe para não obrigar o operador a percorrer médico por médico numa unidade de
    /// 113 profissionais.
    ///
    /// <para>Não vai ao SISREG: mexe só no que já está mapeado aqui. Devolve o que passou a valer
    /// para a tela avisar o custo (cada par habilitado é uma requisição por varredura — a menos
    /// que a unidade use o recorte "unidade inteira", que traz a agenda toda numa só).</para>
    /// </summary>
    Task<AlternarTudoDaUnidadeDto> AlternarTudoDaUnidadeAsync(
        bool habilitados, bool? enviarConfirmacao, CancellationToken cancellationToken = default);

    /// <summary>
    /// Aplica de uma vez, a um profissional, o habilita/desabilita do médico e de todos os seus
    /// procedimentos (<paramref name="habilitados"/>) e o aviso por WhatsApp
    /// (<paramref name="enviarConfirmacao"/>). Automatiza o "clique-clique" da tela sem mudar
    /// nenhuma semântica: o zap segue a regra do procedimento na unidade (ADR-0040), alcançando
    /// as demais linhas do mesmo código.
    /// </summary>
    Task AlternarProcedimentosDoProfissionalAsync(
        Guid profissionalId, bool habilitados, bool enviarConfirmacao, CancellationToken cancellationToken = default);

    /// <summary>
    /// Núcleo do <see cref="SincronizarFhirAsync"/> com unidade e autor explícitos — para o motor em
    /// lote rodar fora de uma request.
    /// </summary>
    Task<SisregSincronizacaoFhirDto> SincronizarFhirNoContextoAsync(
        Unidade unidade, Guid? usuarioId, CancellationToken cancellationToken = default);
}

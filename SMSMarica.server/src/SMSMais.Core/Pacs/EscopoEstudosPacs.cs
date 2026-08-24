using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SMSMais.Core.Common.Unidades;
using SMSMais.Core.Identidade;
using SMSMais.Data;

namespace SMSMais.Core.Pacs;

/// <summary>
/// Traduz o escopo de unidade do usuário (ADR-0033/0037) para o recorte que o PACS entende:
/// o conjunto de <b>AE Titles</b> dos equipamentos daquelas unidades.
///
/// <para>O dcm4chee não sabe o que é "unidade" — mas sabe de qual AE cada série veio, e guarda
/// isso na tag privada <c>(7777,1037) SendingApplicationEntityTitleOfSeries</c>, que é uma chave
/// de BUSCA válida no QIDO (verificado no 5.34.3 de produção: multi-valor por vírgula faz união
/// e combina com <c>ModalitiesInStudy</c>/<c>StudyDate</c>/<c>offset</c> sem quebrar a paginação).
/// Como o AE de origem é o <see cref="SMSMais.Data.Entities.Equipamento.IdentificadorDicom"/> que já
/// cadastramos, e o equipamento pertence a uma unidade, o filtro por unidade sai de graça — e
/// vale inclusive para o estudo <b>órfão</b>, que ainda não casou com nenhuma solicitação: o AE
/// viaja na imagem, associada ou não.</para>
///
/// <para><b>DESLIGADO por padrão</b> (<c>Pacs:EscopoPorUnidade:Habilitado</c>), e o motivo é
/// concreto: <b>estudo reescrito perde o AE</b>. Toda associação manual reescreve o objeto DICOM
/// (ver <c>ICorrecaoIdentidadeExameService</c>) e o re-armazena por STOW-RS, que não tem AE
/// chamador — o dcm4chee grava a tag vazia. Medido em 20/08/2026: 20 estudos do CDT, todos de
/// agosto e 4 do mesmo dia, ficaram sem AE nenhum; e como o US do CDT não consome a worklist e é
/// associado à mão todo dia, esse conjunto CRESCE. Filtrar por AE hoje esconderia
/// exatamente os exames que a recepção acabou de conciliar. O QIDO não sabe dizer "AE no conjunto
/// OU AE ausente" (testado: vírgula solta é ignorada e curinga vira universal), então fechar isso
/// exige a lista sair do nosso banco — onde o estudo reescrito É conhecido, com unidade e tudo.</para>
///
/// <para><b>Só vale para a VISTA.</b> O motor de conciliação (<c>SincronizadorExamesService</c>)
/// vai direto ao dcm4chee por <c>IConsultaStudyClient</c>, sem passar pelo proxy e sem
/// <c>HttpContext</c> — logo cai no passo 1 de <see cref="EscopoUnidade"/> ("sem usuário ⇒ vê
/// tudo") e continua varrendo a rede inteira. Errar o mapa AE→unidade faz alguém deixar de VER
/// uma linha; nunca faz um exame deixar de ser conciliado.</para>
/// </summary>
public interface IEscopoEstudosPacs
{
    Task<EscopoAeResultado> ResolverAsync(CancellationToken cancellationToken = default);
}

/// <param name="SemRestricao">Não aplicar filtro nenhum (acesso global ou execução sem usuário).</param>
/// <param name="AeTitles">AEs visíveis quando há restrição. Vazio com <paramref name="SemRestricao"/>
/// <c>false</c> significa <b>nada visível</b> — use <see cref="SemAcesso"/> em vez de checar o tamanho.</param>
public sealed record EscopoAeResultado(bool SemRestricao, string[] AeTitles)
{
    public static readonly EscopoAeResultado Tudo = new(true, []);

    /// <summary>Nenhum estudo visível — fail-closed (ADR-0037).</summary>
    public static readonly EscopoAeResultado Nada = new(false, []);

    public bool SemAcesso => !SemRestricao && AeTitles.Length == 0;
}

internal sealed class EscopoEstudosPacs(
    SmsMaisDbContext db, IUsuarioAtualAccessor usuarioAtual, IConfiguration configuration)
    : IEscopoEstudosPacs
{
    /// <summary>Chave de configuração que liga o recorte. Ver a ressalva do AE ausente no resumo.</summary>
    public const string ChaveHabilitado = "Pacs:EscopoPorUnidade:Habilitado";

    public async Task<EscopoAeResultado> ResolverAsync(CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue(ChaveHabilitado, false)) return EscopoAeResultado.Tudo;

        var escopo = await EscopoUnidade.ResolverAsync(db, usuarioAtual, cancellationToken);
        if (escopo.VeTudo) return EscopoAeResultado.Tudo;
        if (escopo.SemAcesso) return EscopoAeResultado.Nada;

        var unidades = escopo.Unidades;
        var aes = await db.Equipamentos.AsNoTracking()
            .Where(e => unidades.Contains(e.UnidadeId)
                        && e.ExcluidoEm == null
                        && e.IdentificadorDicom != null
                        && e.IdentificadorDicom != "")
            .Select(e => e.IdentificadorDicom!)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        // Unidade sem NENHUM equipamento com AE não executa imagem: fecha, não abre. Devolver
        // "sem restrição" aqui transformaria a falta de cadastro na permissão mais ampla do
        // sistema — exatamente o que o ADR-0037 inverteu.
        return aes.Length == 0 ? EscopoAeResultado.Nada : new EscopoAeResultado(false, aes);
    }
}

using Microsoft.EntityFrameworkCore;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Data;

namespace SMSMais.Core.Pacientes.Unificacao;

/// <summary>
/// Reaponta, dentro de <c>smsmarica.*</c>, toda coluna que guarda o id de um Patient do hub FHIR
/// (sem FK — ADR-0010/0039) do paciente ABSORVIDO para o SOBREVIVENTE quando dois cadastros são
/// unificados. O <c>$merge</c> do hub só alcança <c>fhir.*</c> (o clínico); esta é a outra metade,
/// e sem ela laudo/solicitação/conversa/… ficam apontando para o cadastro que foi absorvido.
///
/// <para><b>O registro de colunas é EXPLÍCITO e curado de propósito.</b> Uma varredura cega por
/// "paciente_id" repontaria FKs que apontam para OUTRA tabela — <c>comunicacao_paciente_id</c> →
/// <c>comunicacao_paciente</c>, <c>documento_paciente_id</c> → <c>documento_paciente</c> — e
/// fundiria dado errado. Snapshots denormalizados (<c>paciente_nome</c>/<c>paciente_cpf</c>/
/// <c>paciente_cns</c>) ficam intocados de propósito: são a foto do momento em que a linha nasceu.
/// A trilha de divergência do PEP (<c>pep_sincronizacao_divergencia.patient_id_hub</c>) também fica
/// de fora — é histórico append-only, não se reescreve.</para>
/// </summary>
public interface IRepontadorPacienteService
{
    /// <summary>
    /// Quantas linhas de cada tabela apontam HOJE para <paramref name="pacienteId"/> — a prévia
    /// (dry-run) do que a unificação vai mover. Só devolve as tabelas com pelo menos uma linha.
    /// </summary>
    Task<IReadOnlyList<ContagemRepontamento>> ContarReferenciasAsync(
        Guid pacienteId, CancellationToken ct = default);

    /// <summary>
    /// Reaponta tudo de <paramref name="absorvidoId"/> para <paramref name="sobreviventeId"/> numa
    /// única transação. Idempotente: rodar de novo é no-op (nada mais aponta para o absorvido), o
    /// que torna a operação segura de repetir se a fusão no hub já tiver passado.
    /// </summary>
    Task<IReadOnlyList<ContagemRepontamento>> RepontarAsync(
        Guid sobreviventeId, Guid absorvidoId, CancellationToken ct = default);
}

/// <summary>Quantas linhas de uma coluna apontam para o paciente (uma entrada por tabela/coluna).</summary>
public sealed record ContagemRepontamento(string Tabela, string Coluna, int Linhas);

public sealed class RepontadorPacienteService(SmsMaisDbContext db) : IRepontadorPacienteService
{
    /// <summary>
    /// (tabela, coluna) — todas em <c>smsmarica</c>, do tipo <c>uuid</c>, referência direta a
    /// <c>fhir.patient</c>. Lista curada à mão (ver doc da interface). Ao criar entidade nova com
    /// id de paciente, acrescente aqui — o teste <c>RepontadorCoberturaTests</c> cobra as colunas
    /// do modelo que parecem id de paciente e não estão nesta lista (nem em
    /// <see cref="ForaDeProposito"/>).
    /// </summary>
    internal static readonly IReadOnlyList<(string Tabela, string Coluna)> Alvos =
    [
        ("acompanhante", "paciente_id"),
        ("acompanhante", "paciente_vinculado_id"),
        ("agendamento_externo", "paciente_id"),
        ("anexo_upload_token", "patient_id"),
        ("cadsus_completude", "paciente_destino_id"),
        ("cidadao_acesso", "patient_id"),
        ("cidadao_login_link", "patient_id"),
        ("cidadao_notificacao", "paciente_id"),
        ("comunicacao_paciente", "paciente_id"),
        ("contato_comprometido", "paciente_id"),
        ("contato_registro", "paciente_id"),
        ("conversa", "paciente_id"),
        ("dispensa_verificacao_contato", "paciente_id"),
        ("documento_paciente", "paciente_id"),
        ("esussg_solicitacao", "paciente_id"),
        ("exame_associacao", "paciente_id"),
        ("guia_agendamento", "paciente_id"),
        ("laudo", "paciente_id"),
        ("ouvidoria_manifestacao", "manifestante_patient_id"),
        ("ouvidoria_manifestacao", "referido_patient_id"),
        ("pendencia_cadastro", "paciente_id"),
        ("personificacao_paciente", "patient_id"),
        ("pesquisa_satisfacao", "patient_id"),
        ("regulacao_analise_espelho", "paciente_id"),
        ("regulacao_solicitacao", "paciente_id"),
        ("robo_tarefa", "paciente_id"),
        ("ser_solicitacao", "paciente_id"),
        ("sernit_solicitacao", "paciente_id"),
        ("solicitacao", "paciente_id"),
        ("tfd_registro_faturamento", "paciente_id"),
        ("tratamento", "paciente_id"),
        ("verificacao_cadastral_estado", "paciente_id"),
        ("whatsapp_mensagem", "paciente_id"),
    ];

    /// <summary>
    /// Colunas com cara de id de paciente que NÃO são repontadas, cada uma com o porquê. O teste de
    /// cobertura aceita estas; qualquer outra coluna nova fora de <see cref="Alvos"/> o derruba.
    /// </summary>
    internal static readonly IReadOnlyDictionary<(string Tabela, string Coluna), string> ForaDeProposito =
        new Dictionary<(string, string), string>
        {
            [("agendamento_confirmacao_estado", "comunicacao_paciente_id")] =
                "FK para comunicacao_paciente (a comunicação é que é repontada).",
            [("verificacao_cadastral_estado", "comunicacao_paciente_id")] =
                "FK para comunicacao_paciente (a comunicação é que é repontada).",
            [("guia_agendamento", "documento_paciente_id")] =
                "FK para documento_paciente (o documento é que é repontado).",
            [("whatsapp_mensagem", "documento_paciente_id")] =
                "FK para documento_paciente (o documento é que é repontado).",
            [("exame_imagem", "liberado_paciente_por")] =
                "Id do USUÁRIO que liberou o exame ao paciente, não do paciente.",
        };

    public async Task<IReadOnlyList<ContagemRepontamento>> ContarReferenciasAsync(
        Guid pacienteId, CancellationToken ct = default)
    {
        var resultado = new List<ContagemRepontamento>();
        foreach (var (tabela, coluna) in Alvos)
        {
            var sql = $"SELECT count(*)::int AS \"Value\" FROM smsmarica.{Ident(tabela)} WHERE {Ident(coluna)} = {{0}}";
            var linhas = await db.Database.SqlQueryRaw<int>(sql, pacienteId).SingleAsync(ct);
            if (linhas > 0) resultado.Add(new ContagemRepontamento(tabela, coluna, linhas));
        }
        return resultado;
    }

    /// <summary>
    /// Condição extra do <c>UPDATE</c> de uma tabela ({0} = sobrevivente). Só o acesso ao app precisa:
    /// o índice único é sobre <c>patient_id</c> sem filtro, então, se o sobrevivente já tem acesso,
    /// o do absorvido (desativado em <see cref="ResolverUnicosAsync"/>) fica onde está.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> GuardaPorTabela =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["cidadao_acesso"] = "NOT EXISTS (SELECT 1 FROM smsmarica.cidadao_acesso x WHERE x.patient_id = {0})",
        };

    public async Task<IReadOnlyList<ContagemRepontamento>> RepontarAsync(
        Guid sobreviventeId, Guid absorvidoId, CancellationToken ct = default)
    {
        if (sobreviventeId == absorvidoId)
            throw new ValidacaoException(
                "paciente.unificar_mesmo", "Sobrevivente e absorvido não podem ser o mesmo paciente.");

        var resultado = new List<ContagemRepontamento>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await ResolverUnicosAsync(sobreviventeId, absorvidoId, ct);
        foreach (var (tabela, coluna) in Alvos)
        {
            var guarda = GuardaPorTabela.TryGetValue(tabela, out var g) ? $" AND {g}" : "";
            var sql = $"UPDATE smsmarica.{Ident(tabela)} SET {Ident(coluna)} = {{0}} WHERE {Ident(coluna)} = {{1}}{guarda}";
            var linhas = await db.Database.ExecuteSqlRawAsync(
                sql, [sobreviventeId, absorvidoId], ct);
            if (linhas > 0) resultado.Add(new ContagemRepontamento(tabela, coluna, linhas));
        }
        await tx.CommitAsync(ct);
        return resultado;
    }

    /// <summary>
    /// Quatro tabelas têm índice único que inclui o paciente. Quando os DOIS cadastros já têm a
    /// linha "única", mover a do absorvido estoura 23505 e derruba a unificação inteira — por isso
    /// a duplicata do absorvido é encerrada antes, pelo caminho que cada tabela já tem para isso
    /// (nada é apagado; a linha fica como histórico, apontando para o cadastro que saiu):
    /// <list type="bullet">
    /// <item><c>cidadao_acesso</c> (um acesso ao app por paciente): o do absorvido é desativado e as
    /// sessões abertas dele caem; consentimentos LGPD ficam com ele, como histórico. A pessoa segue
    /// entrando no app pelo acesso do cadastro que ficou.</item>
    /// <item><c>dispensa_verificacao_contato</c> (uma ativa por paciente): a do absorvido é revogada.</item>
    /// <item><c>contato_comprometido</c> (uma aberta por paciente+telefone+motivo): a do absorvido é
    /// resolvida — a mesma pendência segue aberta no cadastro que ficou.</item>
    /// <item><c>acompanhante</c> (um ativo por paciente+CPF): o do absorvido é excluído logicamente —
    /// o mesmo acompanhante já está no cadastro que ficou.</item>
    /// </list>
    /// O acesso ao app é o único que não muda de dono depois: ver <see cref="GuardaPorTabela"/>.
    /// </summary>
    private async Task ResolverUnicosAsync(Guid s, Guid a, CancellationToken ct)
    {
        const string nota = "Unificação de cadastros: o cadastro que ficou já tinha este registro.";

        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE smsmarica.cidadao_sessao SET revogada_em = now()
             WHERE revogada_em IS NULL
               AND cidadao_acesso_id IN (SELECT id FROM smsmarica.cidadao_acesso WHERE patient_id = {1})
               AND EXISTS (SELECT 1 FROM smsmarica.cidadao_acesso WHERE patient_id = {0})
            """, [s, a], ct);
        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE smsmarica.cidadao_acesso SET ativo = false, atualizado_em = now()
             WHERE patient_id = {1}
               AND EXISTS (SELECT 1 FROM smsmarica.cidadao_acesso WHERE patient_id = {0})
            """, [s, a], ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE smsmarica.dispensa_verificacao_contato SET revogado_em = now(), revogado_motivo = {2}
             WHERE paciente_id = {1} AND revogado_em IS NULL
               AND EXISTS (SELECT 1 FROM smsmarica.dispensa_verificacao_contato
                            WHERE paciente_id = {0} AND revogado_em IS NULL)
            """, [s, a, nota], ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE smsmarica.contato_comprometido c SET resolvido_em = now(), resolucao_nota = {2}
             WHERE c.paciente_id = {1} AND c.resolvido_em IS NULL
               AND EXISTS (SELECT 1 FROM smsmarica.contato_comprometido x
                            WHERE x.paciente_id = {0} AND x.resolvido_em IS NULL
                              AND x.telefone_canonical = c.telefone_canonical AND x.motivo = c.motivo)
            """, [s, a, nota], ct);

        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE smsmarica.acompanhante c SET excluido_em = now()
             WHERE c.paciente_id = {1} AND c.excluido_em IS NULL
               AND EXISTS (SELECT 1 FROM smsmarica.acompanhante x
                            WHERE x.paciente_id = {0} AND x.excluido_em IS NULL AND x.cpf = c.cpf)
            """, [s, a], ct);
    }

    /// <summary>
    /// Defesa em profundidade: os identificadores vêm só de <see cref="Alvos"/> (constantes de
    /// código, não entrada de usuário), mas interpolar identificador em SQL exige garantir que
    /// ninguém introduza algo fora de <c>[a-z_]</c> ao editar a lista.
    /// </summary>
    private static string Ident(string bruto)
    {
        foreach (var c in bruto)
        {
            if (!(c is >= 'a' and <= 'z' || c == '_'))
                throw new InvalidOperationException($"Identificador SQL inválido no repontador: '{bruto}'.");
        }
        return bruto;
    }
}

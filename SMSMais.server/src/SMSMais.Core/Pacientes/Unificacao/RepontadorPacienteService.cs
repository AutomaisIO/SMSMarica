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
    /// que existem no banco e não estão nesta lista.
    /// </summary>
    internal static readonly IReadOnlyList<(string Tabela, string Coluna)> Alvos =
    [
        ("acompanhante", "paciente_id"),
        ("acompanhante", "paciente_vinculado_id"),
        ("anexo_upload_token", "patient_id"),
        ("cadsus_completude", "paciente_destino_id"),
        ("cidadao_acesso", "patient_id"),
        ("cidadao_login_link", "patient_id"),
        ("comunicacao_paciente", "paciente_id"),
        ("contato_comprometido", "paciente_id"),
        ("contato_registro", "paciente_id"),
        ("conversa", "paciente_id"),
        ("dispensa_verificacao_contato", "paciente_id"),
        ("documento_paciente", "paciente_id"),
        ("esussg_solicitacao", "paciente_id"),
        ("exame_associacao", "paciente_id"),
        ("laudo", "paciente_id"),
        ("ouvidoria_manifestacao", "manifestante_patient_id"),
        ("ouvidoria_manifestacao", "referido_patient_id"),
        ("pendencia_cadastro", "paciente_id"),
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

    public async Task<IReadOnlyList<ContagemRepontamento>> RepontarAsync(
        Guid sobreviventeId, Guid absorvidoId, CancellationToken ct = default)
    {
        if (sobreviventeId == absorvidoId)
            throw new ValidacaoException(
                "paciente.unificar_mesmo", "Sobrevivente e absorvido não podem ser o mesmo paciente.");

        var resultado = new List<ContagemRepontamento>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        foreach (var (tabela, coluna) in Alvos)
        {
            var sql = $"UPDATE smsmarica.{Ident(tabela)} SET {Ident(coluna)} = {{0}} WHERE {Ident(coluna)} = {{1}}";
            var linhas = await db.Database.ExecuteSqlRawAsync(
                sql, [sobreviventeId, absorvidoId], ct);
            if (linhas > 0) resultado.Add(new ContagemRepontamento(tabela, coluna, linhas));
        }
        await tx.CommitAsync(ct);
        return resultado;
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

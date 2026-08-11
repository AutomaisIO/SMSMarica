namespace SMSMarica.Core.Associacoes;

/// <summary>Como o exame que estava com o estudo errado deve ser liberado.</summary>
public enum DestinoDoExameDeOrigem
{
    /// <summary>Volta para a worklist — o paciente AINDA NÃO fez o exame dele.</summary>
    DevolverAWorklist = 1,

    /// <summary>Fica liberado sem voltar à worklist — o exame já foi feito e vai ser resolvido
    /// por associação em seguida. Sem esta opção o paciente reaparece na estação por alguns
    /// minutos e a técnica vê alguém que não deve ser examinado.</summary>
    JaFoiFeito = 2,
}

/// <summary>O que existe hoje no estudo e no exame de destino — alimenta a confirmação da tela.</summary>
public sealed record PreviaCorrecaoDto(
    string StudyInstanceUID,
    string? PacienteAtualNome,
    Guid? ExameAtualId,
    string? AccessionAtual,
    string DestinoPacienteNome,
    Guid DestinoExameId,
    string DestinoAccession,
    string? DestinoProcedimento,
    int RascunhosQueSeraoDescartados,
    bool TemLaudoAssinado,
    bool ComunicacaoJaEnviada,
    /// <summary>Estudo que o destino já possui (ou órfão que parece ser dele) — a sugestão da
    /// opção "trocar". Null quando não há candidato e o caso é de "alterar destino".</summary>
    string? StudyDoDestinoSugerido);

public sealed record DescartarEstudoRequest(string StudyInstanceUID, string Motivo);

public sealed record AlterarDestinoRequest(
    string StudyInstanceUID, string AccessionDestino, DestinoDoExameDeOrigem DestinoDaOrigem, string Motivo);

public sealed record TrocarEstudosRequest(
    string StudyInstanceUID, string AccessionDestino, string StudyInstanceUIDDoDestino, string Motivo);

/// <summary>
/// Correção de IDENTIDADE de exame: o estudo está no paciente errado.
///
/// <para>Nasce do incidente de 11/08/2026 — a técnica selecionou o item de worklist errado, o
/// equipamento copiou dali PatientName/PatientID/AccessionNumber, e as imagens de uma paciente
/// foram arquivadas sob outro. Como as chaves voltam <b>coerentes entre si</b>, a conciliação
/// aceita o estudo sem nenhum sinal de erro.</para>
///
/// <para>Não é "desassociar": quando o estudo volta com o StudyInstanceUID pré-gerado, o vínculo é
/// <b>implícito</b> — mora na coluna <c>exame_imagem.study_instance_uid</c>, que é UNIQUE — e não
/// existe linha em <c>exame_associacao</c> para apagar.</para>
///
/// <para>Toda operação <b>reescreve o objeto DICOM</b> (ver <c>IPacsReescritorEstudoClient</c>):
/// coagir os atributos corrigiria PatientID e AccessionNumber mas deixaria o nome de outra pessoa
/// dentro do arquivo.</para>
/// </summary>
public interface ICorrecaoIdentidadeExameService
{
    /// <summary>Prévia da correção. <paramref name="accessionDestino"/> nulo = só o estado atual.</summary>
    Task<PreviaCorrecaoDto> ObterPreviaAsync(
        string studyInstanceUID, string? accessionDestino, CancellationToken cancellationToken = default);

    /// <summary>Opção 1 — o estudo é lixo: rejeita (IOCM) e apaga; o exame volta à worklist.</summary>
    Task DescartarEstudoAsync(DescartarEstudoRequest request, CancellationToken cancellationToken = default);

    /// <summary>Opção 2 — o estudo é de outra pessoa: reescreve para o destino e libera a origem.</summary>
    Task AlterarDestinoAsync(AlterarDestinoRequest request, CancellationToken cancellationToken = default);

    /// <summary>Opção 3 — cada estudo está no exame do outro: reescreve os dois de uma vez.</summary>
    Task TrocarAsync(TrocarEstudosRequest request, CancellationToken cancellationToken = default);
}

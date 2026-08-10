using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SMSMarica.Data;
using SMSMarica.Data.Entities;
using SMSMarica.Data.Entities.Enums;

namespace SMSMarica.Core.Integracoes.SisregWeb.Importacao;

/// <summary>
/// Traduz o procedimento que o SISREG informou num <see cref="TipoExame"/>, criando-o se ainda não
/// existir. É o que faz o exame de imagem entrar já nomeado, sem operador nenhum no meio.
///
/// <para><b>A chave é o NOME, não o código.</b> O <c>pa</c> é a identificação natural e seria a
/// escolha óbvia, mas a medição da produção em 10/08/2026 desmente: das 3.096 solicitações com RAW,
/// <b>1.018 (33%) vêm com a coluna do <c>pa</c> vazia</b>, e são 20 procedimentos que aparecem ora
/// com código, ora sem — inclusive o mesmo procedimento nas duas formas. Chavear pelo código
/// criaria um tipo "sem código" paralelo a cada procedimento real. O nome, esse nunca falta.</para>
///
/// <para><b>O nome é confiável como chave</b> na mesma medição: nenhum nome do SISREG aparece
/// associado a dois códigos SIGTAP diferentes. O código é gravado e respeitado quando vem, e
/// preenchido depois se o tipo já existia sem ele.</para>
///
/// <para><b>O código "SIGTAP" que o SISREG exporta não é o SIGTAP oficial.</b> Ele vem de uma
/// versão defasada da tabela: consultado no catálogo oficial devolve outro procedimento. Medido em
/// 10/08/2026 — o 0205020127 que o SISREG usa para "ULTRASONOGRAFIA DE TIREOIDE" é
/// "ULTRASSONOGRAFIA PÉLVICA (GINECOLÓGICA)" no oficial, e o 0205020178 de "TRANSFONTANELAR -
/// INFANTIL" é "DE TIREOIDE". Foi assim que o exame de dois recém-nascidos foi parar no aparelho
/// rotulado como tireoide. Por isso <b>nada aqui liga o tipo ao catálogo SIGTAP</b>: só o SUBGRUPO
/// (4 primeiros dígitos), que a defasagem não move, é aproveitado — e apenas como palpite de
/// modalidade. A correlação de faturamento é trabalho à parte.</para>
/// </summary>
public interface IResolvedorTipoExameSisreg
{
    /// <summary>
    /// Id do tipo de exame para este procedimento do SISREG, criando-o se preciso. Devolve null
    /// só quando o SISREG não informou nome nenhum — aí não há o que nomear.
    /// </summary>
    /// <param name="nomeSisreg">Procedimento em texto, como o SISREG o escreve.</param>
    /// <param name="codigoSisreg">O <c>pa</c>. Opcional: vem vazio em boa parte das linhas.</param>
    /// <param name="codigoSigtap">
    /// Usado <b>só para inferir a modalidade pelo SUBGRUPO</b> (os 4 primeiros dígitos). O código
    /// inteiro não serve para nada aqui — ver a nota da classe.
    /// </param>
    Task<Guid?> ResolverOuCriarAsync(
        string? nomeSisreg, string? codigoSisreg, string? codigoSigtap, CancellationToken ct);
}

public sealed class ResolvedorTipoExameSisreg(
    SmsMaricaDbContext db,
    ILogger<ResolvedorTipoExameSisreg> logger) : IResolvedorTipoExameSisreg
{
    private const int TamanhoMaximoNome = 200;

    public async Task<Guid?> ResolverOuCriarAsync(
        string? nomeSisreg, string? codigoSisreg, string? codigoSigtap, CancellationToken ct)
    {
        var nome = NormalizarNome(nomeSisreg ?? string.Empty);
        if (nome.Length == 0) return null;

        var codigo = SoDigitos(codigoSisreg);
        var sigtap = SoDigitos(codigoSigtap);

        var existente = await db.TiposExame
            .FirstOrDefaultAsync(t => t.Nome == nome && t.ExcluidoEm == null, ct);

        if (existente is not null)
        {
            CompletarLacunas(existente, codigo);
            return existente.Id;
        }

        var agora = DateTime.UtcNow;

        var tipo = new TipoExame
        {
            Id = Guid.CreateVersion7(),
            Nome = nome,
            CodigoSisreg = codigo.Length > 0 ? codigo : null,
            AutoCriado = true,
            // Null de propósito — o código exportado pelo SISREG não é o SIGTAP oficial.
            ProcedimentoSigtapId = null,
            ModalidadeDicom = InferirModalidade(sigtap),
            // O worklist item quer texto curto e descritivo — o nome do SISREG já é exatamente isso.
            RequestedProcedureDescription = nome,
            ScheduledProcedureStepDescription = nome,
            CodigosProtocolo = [],
            Ativo = true,
            // NASCE DESLIGADO. Falta a configuração DICOM (modalidade, equipamento, protocolo) e
            // mandar worklist item mal formado ao equipamento é pior do que não mandar.
            EnviarParaWorklist = false,
            CriadoEm = agora,
            // CriadoPor fica null de propósito: não foi pessoa nenhuma, foi a importação.
        };

        db.TiposExame.Add(tipo);

        logger.LogInformation(
            "TIPO_EXAME_AUTO: criado \"{Nome}\" (SISREG {Codigo}, SIGTAP {Sigtap}) a partir da "
            + "importação. Envio à worklist DESLIGADO até alguém configurar o DICOM.",
            nome, codigo.Length > 0 ? codigo : "(vazio)", sigtap.Length > 0 ? sigtap : "(vazio)");

        return tipo.Id;
    }

    /// <summary>
    /// Nome do SISREG na forma canônica: MAIÚSCULAS, sem espaço sobrando. O colapso de espaços
    /// importa — o export traz "CONSULTA  EM CARDIOLOGIA - PEDIATRIA" com espaço duplo, e sem isto
    /// a mesma coisa viraria dois tipos.
    /// </summary>
    public static string NormalizarNome(string bruto)
    {
        var limpo = string.Join(' ', bruto.ToUpperInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return limpo.Length > TamanhoMaximoNome ? limpo[..TamanhoMaximoNome] : limpo;
    }

    /// <summary>
    /// Preenche o que o tipo ainda não sabia — hoje, só o código do SISREG, que costuma chegar
    /// numa linha depois de o tipo já existir sem ele. Nunca SOBRESCREVE: o que já está gravado foi
    /// decidido antes (por outra linha ou por um operador) e uma linha nova não vira essa decisão.
    /// </summary>
    private void CompletarLacunas(TipoExame tipo, string codigo)
    {
        if (tipo.CodigoSisreg is null && codigo.Length > 0)
        {
            tipo.CodigoSisreg = codigo;
            tipo.AtualizadoEm = DateTime.UtcNow;
        }
        else if (tipo.CodigoSisreg is { } atual && codigo.Length > 0 && atual != codigo)
        {
            // Mesmo nome com dois `pa` de verdade não aconteceu em produção até 10/08/2026 (só
            // vazio × preenchido). Se acontecer, é sinal de que o nome deixou de bastar como chave —
            // e isso é decisão de gente, não de importação.
            logger.LogWarning(
                "TIPO_EXAME_PA_DIVERGENTE: \"{Nome}\" está com o código SISREG {Atual} e chegou uma "
                + "linha com {Novo}. Mantido o atual.", tipo.Nome, atual, codigo);
        }
    }

    /// <summary>
    /// Palpite de modalidade pelo SUBGRUPO (4 primeiros dígitos), só para poupar digitação de quem
    /// vai configurar. O subgrupo é a única parte do código exportado que a defasagem do SISREG não
    /// move — os dois casos medidos ("tireoide"/"pélvica" e "transfontanelar"/"tireoide") escorregam
    /// dentro do mesmo 020502. Não é gate: com o envio à worklist desligado, um palpite errado não
    /// chega a equipamento nenhum. O que não encaixa vira
    /// <see cref="ModalidadeDicom.Indefinida"/> em vez de chutar.
    /// </summary>
    private static ModalidadeDicom InferirModalidade(string sigtapSoDigitos)
    {
        if (sigtapSoDigitos.Length < 4) return ModalidadeDicom.Indefinida;

        // Mamografia é 0204 03 — precisa vir antes da regra geral de 0204 (RX).
        if (sigtapSoDigitos.Length >= 6 && sigtapSoDigitos[..6] == "020403") return ModalidadeDicom.MG;

        return sigtapSoDigitos[..4] switch
        {
            "0204" => ModalidadeDicom.DX,
            "0205" => ModalidadeDicom.US,
            "0206" => ModalidadeDicom.CT,
            "0207" => ModalidadeDicom.MR,
            _ => ModalidadeDicom.Indefinida,
        };
    }

    private static string SoDigitos(string? valor) =>
        string.IsNullOrEmpty(valor) ? string.Empty : new string([.. valor.Where(char.IsDigit)]);
}

using System.Globalization;
using System.Text.RegularExpressions;
using SMSMarica.Core.Integracoes.SisregWeb.Importacao;

namespace SMSMarica.Core.Integracoes.SisregWeb.Varredura;

/// <summary>
/// Converte o registro observado na agenda (<see cref="RegistroVarreduraRaw"/>) na
/// <see cref="MarcacaoSisreg"/> que o fluxo de importação já sabe processar. Puro: sem I/O, sem
/// relógio — o SIGTAP entra como parâmetro, resolvido fora.
/// </summary>
public static partial class VarreduraMapper
{
    /// <param name="codigoSigtapResolvido">
    /// SIGTAP (só dígitos) do de-para CONFIRMADO para o <c>pa</c> do registro, ou null se ainda não
    /// mapeado. Entra por parâmetro justamente para o reprocesso reresolver: o RAW é observação, o
    /// de-para é configuração, e configuração muda depois que a pendência foi criada.
    /// </param>
    public static MarcacaoSisreg ParaMarcacao(RegistroVarreduraRaw raw, string? codigoSigtapResolvido)
    {
        var (municipio, _) = SepararOrigem(raw.Origem);

        return new MarcacaoSisreg(
            CodigoSolicitacao: raw.CoSolicitacao.Trim(),
            CnsPaciente: Limpo(raw.Cns),
            NomePaciente: Limpo(raw.Paciente),
            ProcedimentoTexto: LimparProcedimento(raw.Procedimentos) ?? Limpo(raw.PaNome),
            CodigoSigtap: codigoSigtapResolvido,

            // A agenda é a visão do EXECUTANTE: o médico solicitante não aparece na listagem, e
            // buscar a ficha de cada registro custaria +1 requisição por agendamento — sozinho isso
            // estouraria o limite anti-robô. Fica nulo, e isso é degradação visível, não silenciosa.
            CpfMedicoSolicitante: null,
            NomeMedicoSolicitante: null,
            CrmMedicoSolicitante: null,

            CnesUnidadeSolicitante: Limpo(raw.CnesSolicitante),
            NomeUnidadeSolicitante: Limpo(raw.UnidadeSolicitante),
            CnesUnidadeExecutante: Limpo(raw.CnesExecutante),
            NomeUnidadeExecutante: Limpo(raw.NomeExecutante),
            DataHoraAtendimento: MontarDataHora(raw.Data, raw.Hora),

            // Só o TXT (expo_solicitacoes) traz estas duas — a tela de agenda não as exibe.
            DataSolicitacao: null,
            DataRegulacao: null,

            Cid: Limpo(raw.Cid10),
            TelefonePaciente: PrimeiroTelefone(raw.Telefones),

            // Endereço não existe na agenda. Paciente novo criado pela varredura nasce com a
            // demografia do CADSUS, sem logradouro.
            TipoLogradouro: null,
            Logradouro: null,
            Complemento: null,
            Numero: null,
            Bairro: null,
            Cep: null,
            MunicipioResidencia: municipio,
            CodigoIbgeResidencia: null,

            LinhaRaw: raw.Serializar(),

            NascimentoPaciente: LerData(raw.Nascimento),
            SituacaoAgendamento: Limpo(raw.Situacao),
            VagaSolicitada: Limpo(raw.VagaSolicitada),
            VagaConsumida: Limpo(raw.VagaConsumida),
            CpfProfissionalExecutante: Limpo(raw.ProfCpf),
            NomeProfissionalExecutante: Limpo(raw.ProfNome),
            CodigoProcedimentoSisreg: Limpo(raw.PaCodigo));
    }

    /// <summary>
    /// <c>"01 - MAMOGRAFIA BILATERAL"</c> → <c>"MAMOGRAFIA BILATERAL"</c>. O número é a ordem do
    /// procedimento dentro da solicitação, não faz parte do nome.
    /// </summary>
    internal static string? LimparProcedimento(string? valor)
    {
        var texto = Limpo(valor);
        if (texto is null) return null;

        var m = PrefixoOrdem().Match(texto);
        return m.Success ? m.Groups[1].Value.Trim() : texto;
    }

    /// <summary><c>"MARICA - RJ"</c> → município e UF.</summary>
    internal static (string? Municipio, string? Uf) SepararOrigem(string? origem)
    {
        var texto = Limpo(origem);
        if (texto is null) return (null, null);

        var partes = texto.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return partes.Length >= 2 ? (partes[0], partes[^1]) : (texto, null);
    }

    /// <summary>
    /// O SISREG pode listar mais de um telefone. Guardamos o primeiro: o telefone principal do
    /// paciente é o verificado por OTP (ADR-0020) e este entra em slot secundário — encher o
    /// cadastro com todos os números da agenda só polui.
    /// </summary>
    internal static string? PrimeiroTelefone(string? telefones)
    {
        var texto = Limpo(telefones);
        if (texto is null) return null;

        var primeiro = texto
            .Split([',', ';', '/', '\n'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(t => t.Any(char.IsDigit));

        return Limpo(primeiro);
    }

    /// <summary>Data + hora do SISREG como wall-clock de Brasília — a conversão para UTC é do
    /// serviço de importação, que já faz isso para o TXT.</summary>
    internal static DateTime? MontarDataHora(string? data, string? hora)
    {
        if (LerData(data) is not { } dia) return null;

        var texto = Limpo(hora);
        if (texto is not null
            && TimeOnly.TryParseExact(texto, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var h))
        {
            return dia.ToDateTime(h);
        }

        return dia.ToDateTime(TimeOnly.MinValue);
    }

    internal static DateOnly? LerData(string? valor)
    {
        var texto = Limpo(valor);
        if (texto is null) return null;

        return DateOnly.TryParseExact(texto, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;
    }

    /// <summary>
    /// Trim que também normaliza o NBSP. O HTML do SISREG usa <c>&amp;nbsp;</c> dentro de rótulos
    /// como "Pendente&amp;nbsp;Confirmação", e <c>string.Trim()</c> sem argumento não remove U+00A0.
    /// </summary>
    private static string? Limpo(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var texto = EspacosRepetidos().Replace(valor.Replace(' ', ' '), " ").Trim();
        return texto.Length == 0 ? null : texto;
    }

    [GeneratedRegex(@"^\d{1,3}\s*-\s*(.+)$")]
    private static partial Regex PrefixoOrdem();

    [GeneratedRegex(@"\s+")]
    private static partial Regex EspacosRepetidos();
}

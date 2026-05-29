namespace SMSMarica.Data.Entities.Fhir.Enums;

/// <summary>
/// Tipo de consentimento (granularidade LGPD para dados de saúde).
/// Não está no FHIR base; é convenção BR.
/// </summary>
public enum ConsentType
{
    Other = 0,
    /// <summary>Exibir foto do paciente em painéis/etiquetas.</summary>
    ExibicaoFoto = 1,
    /// <summary>Enviar dados pra Rede Nacional de Dados em Saúde.</summary>
    CompartilhamentoRnds = 2,
    /// <summary>Atendimento por telemedicina.</summary>
    Telemedicina = 3,
    /// <summary>Uso de dados em pesquisa científica.</summary>
    Pesquisa = 4,
    /// <summary>Marketing / contato pra divulgação.</summary>
    Marketing = 5,
    /// <summary>Compartilhamento com terceiros não-SUS.</summary>
    CompartilhamentoExterno = 6,
    /// <summary>Compartilhamento com terceiros do SUS (outra unidade, outro PEP).</summary>
    CompartilhamentoSus = 7,
    /// <summary>Autorização para acesso de familiar/responsável aos dados.</summary>
    AcessoResponsavel = 8,
}

namespace SMSMais.Core.Integracoes.SisregWeb.Importacao;

/// <summary>
/// Marcação (agendamento) lida do SISREG III por scraping — tela "Agendados pela
/// Regulação" (<c>cons_marcados_reg</c>) enriquecida com a ficha detalhe
/// (<c>EXIBIR_FICHA</c>). Fonte de verdade da importação para <c>SolicitacaoExame</c>.
/// Campos crus (texto do HTML); a normalização/resolução acontece no serviço de importação.
/// </summary>
public sealed record MarcacaoSisreg(
    /// <summary>Código da Solicitação (nº de regulação do SISREG) — chave de idempotência.</summary>
    string CodigoSolicitacao,
    string? CnsPaciente,
    string? NomePaciente,
    /// <summary>Procedimento em TEXTO (ex.: "MAMOGRAFIA BILATERAL").</summary>
    string? ProcedimentoTexto,
    /// <summary>Código SIGTAP (só dígitos, ex.: "0204030030"). Vem direto no TXT — dispensa mapear por texto.</summary>
    string? CodigoSigtap,
    string? CpfMedicoSolicitante,
    string? NomeMedicoSolicitante,
    /// <summary>CRM do solicitante (quase sempre vazio na ficha; derivado depois).</summary>
    string? CrmMedicoSolicitante,
    string? CnesUnidadeSolicitante,
    string? NomeUnidadeSolicitante,
    string? CnesUnidadeExecutante,
    string? NomeUnidadeExecutante,
    /// <summary>Data/hora do atendimento no fuso local (wall-clock), como o SISREG informa.</summary>
    DateTime? DataHoraAtendimento,
    /// <summary>Data em que o exame foi SOLICITADO (dia de calendário, sem hora) — coluna 29 do TXT.</summary>
    DateOnly? DataSolicitacao,
    /// <summary>Data em que a solicitação foi REGULADA/autorizada (dia de calendário) — coluna 31 do TXT.</summary>
    DateOnly? DataRegulacao,
    string? Cid,

    // ---- Dados do paciente vindos do TXT (para criar o paciente novo; opcionais) ----
    string? TelefonePaciente = null,
    string? TipoLogradouro = null,
    string? Logradouro = null,
    string? Complemento = null,
    string? Numero = null,
    string? Bairro = null,
    string? Cep = null,
    string? MunicipioResidencia = null,
    string? CodigoIbgeResidencia = null,

    /// <summary>Linha crua do TXT (as-is) que originou esta marcação — proveniência.
    /// Na VARREDURA guarda o envelope JSON do registro observado (<c>RegistroVarreduraRaw</c>).</summary>
    string? LinhaRaw = null,

    // ---- Só a VARREDURA (cons_agendas) preenche; o TXT não traz nenhum destes ----

    /// <summary>Nascimento do paciente. <b>O TXT não traz</b> — é por isso que a resolução de
    /// pendência por CPF recusa criar paciente novo. Com a varredura, passa a existir.</summary>
    DateOnly? NascimentoPaciente = null,

    /// <summary>Situação no SISREG (ex.: <c>Agendamento/Pendente Confirmação/Executante</c>).
    /// <b>Não confundir</b> com o nosso <c>StatusConfirmacao</c>, que é a confirmação do PACIENTE
    /// por WhatsApp/app (ADR-0034). Este é a recepção registrando comparecimento lá — coisas
    /// diferentes, deliberadamente não mapeadas uma na outra.</summary>
    string? SituacaoAgendamento = null,

    /// <summary>Vaga solicitada no SISREG (<c>1ª VEZ</c> / <c>RETORNO</c>).</summary>
    string? VagaSolicitada = null,

    /// <summary>Vaga consumida (<c>RESERVA</c> / <c>1ª VEZ</c> / <c>RETORNO</c>).</summary>
    string? VagaConsumida = null,

    /// <summary>CPF do profissional EXECUTANTE — o eixo da varredura. Não confundir com o
    /// solicitante: são papéis opostos na mesma solicitação.</summary>
    string? CpfProfissionalExecutante = null,

    string? NomeProfissionalExecutante = null,

    /// <summary>Código do procedimento no SISREG (o <c>pa</c>, 7 dígitos). É a chave do de-para
    /// que resolve o <see cref="CodigoSigtap"/>.</summary>
    string? CodigoProcedimentoSisreg = null);

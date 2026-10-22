using SMSMais.Core.Common.Dtos;
using SMSMais.Core.Pacientes.Dtos;

namespace SMSMais.Core.Pacientes.Unificacao;

/// <summary>
/// Monta o material de EXIBIÇÃO da unificação: a lista de campos divergentes entre os dois
/// cadastros (para a resolução na tela) e o agregado de referências por módulo (para a prévia).
/// Sem efeito colateral — só formatação.
/// </summary>
internal static class UnificacaoResumo
{
    /// <summary>Tabela/coluna do repontador → rótulo amigável do módulo do painel.</summary>
    private static readonly IReadOnlyDictionary<string, string> ModuloPorTabela =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["laudo"] = "Laudos",
            ["solicitacao"] = "Solicitações de exame",
            ["regulacao_solicitacao"] = "Regulação",
            ["regulacao_analise_espelho"] = "Regulação",
            ["ser_solicitacao"] = "Regulação (SER)",
            ["sernit_solicitacao"] = "Regulação (SERNIT)",
            ["esussg_solicitacao"] = "Regulação (ESUS-SG)",
            ["tratamento"] = "Tratamentos",
            ["documento_paciente"] = "Acervo de exames",
            ["exame_associacao"] = "Acervo de exames",
            ["conversa"] = "Conversas (WhatsApp)",
            ["whatsapp_mensagem"] = "Conversas (WhatsApp)",
            ["comunicacao_paciente"] = "Comunicação e contatos",
            ["contato_registro"] = "Comunicação e contatos",
            ["contato_comprometido"] = "Comunicação e contatos",
            ["dispensa_verificacao_contato"] = "Comunicação e contatos",
            ["verificacao_cadastral_estado"] = "Comunicação e contatos",
            ["ouvidoria_manifestacao"] = "Ouvidoria",
            ["cidadao_acesso"] = "App do cidadão",
            ["cidadao_login_link"] = "App do cidadão",
            ["cidadao_notificacao"] = "App do cidadão",
            ["anexo_upload_token"] = "App do cidadão",
            ["pesquisa_satisfacao"] = "Pesquisas de satisfação",
            ["acompanhante"] = "Acompanhantes",
            ["robo_tarefa"] = "Atendimento automático",
            ["pendencia_cadastro"] = "Atendimento automático",
            ["tfd_registro_faturamento"] = "TFD",
            ["cadsus_completude"] = "Memória CADSUS",
        };

    public static IReadOnlyList<ContagemModuloDto> AgruparModulos(IEnumerable<ContagemRepontamento> refs) =>
        [.. refs
            .GroupBy(r => ModuloPorTabela.TryGetValue(r.Tabela, out var m) ? m : r.Tabela)
            .Select(g => new ContagemModuloDto(g.Key, g.Sum(x => x.Linhas)))
            .OrderByDescending(m => m.Quantidade)
            .ThenBy(m => m.Modulo, StringComparer.Ordinal)];

    public static IReadOnlyList<DivergenciaCampoDto> Divergencias(PacienteDto s, PacienteDto a)
    {
        var campos = new List<(string Rotulo, string S, string A)>
        {
            ("Nome completo", N(s.NomeCompleto), N(a.NomeCompleto)),
            ("Nome social", N(s.NomeSocial), N(a.NomeSocial)),
            ("CPF", N(s.Cpf), N(a.Cpf)),
            ("CNS", N(s.Cns), N(a.Cns)),
            ("RG", N(s.Rg), N(a.Rg)),
            ("Nascimento", Data(s.DataNascimento), Data(a.DataNascimento)),
            ("Sexo", s.Sexo.ToString(), a.Sexo.ToString()),
            ("Estado civil", s.EstadoCivil.ToString(), a.EstadoCivil.ToString()),
            ("Raça/cor", s.RacaCor.ToString(), a.RacaCor.ToString()),
            ("Escolaridade", s.Escolaridade.ToString(), a.Escolaridade.ToString()),
            ("Ocupação", N(s.Ocupacao), N(a.Ocupacao)),
            ("Naturalidade", N(s.Naturalidade), N(a.Naturalidade)),
            ("Nacionalidade", N(s.Nacionalidade), N(a.Nacionalidade)),
            ("Nome da mãe", N(s.NomeDaMae), N(a.NomeDaMae)),
            ("Nome do pai", N(s.NomeDoPai), N(a.NomeDoPai)),
            ("Responsável legal", N(s.ResponsavelLegal), N(a.ResponsavelLegal)),
            ("Endereço", End(s.Endereco), End(a.Endereco)),
            ("Telefone principal", N(s.TelefonePrincipal), N(a.TelefonePrincipal)),
            ("Telefone celular", N(s.TelefoneCelular), N(a.TelefoneCelular)),
            ("Telefone residencial", N(s.TelefoneResidencial), N(a.TelefoneResidencial)),
            ("E-mail", N(s.Email), N(a.Email)),
            ("Contato de emergência", Ctt(s.ContatoEmergencia), Ctt(a.ContatoEmergencia)),
            ("Altura (cm)", s.AlturaCm?.ToString() ?? "", a.AlturaCm?.ToString() ?? ""),
            ("Peso (kg)", s.PesoKg?.ToString() ?? "", a.PesoKg?.ToString() ?? ""),
            ("Tipo sanguíneo", s.TipoSanguineo.ToString(), a.TipoSanguineo.ToString()),
            ("Fator Rh", s.FatorRh.ToString(), a.FatorRh.ToString()),
            ("Alergias", Lista(s.Alergias), Lista(a.Alergias)),
            ("Medicamentos contínuos", Lista(s.MedicamentosContinuos), Lista(a.MedicamentosContinuos)),
            ("Comorbidades", Lista(s.Comorbidades), Lista(a.Comorbidades)),
            ("Deficiências", Lista(s.Deficiencias), Lista(a.Deficiencias)),
            ("Plano de saúde", N(s.PlanoSaude), N(a.PlanoSaude)),
            ("Observações", N(s.Observacoes), N(a.Observacoes)),
        };

        return [.. campos
            .Where(c => !string.Equals(c.S, c.A, StringComparison.Ordinal))
            .Select(c => new DivergenciaCampoDto(
                c.Rotulo,
                string.IsNullOrEmpty(c.S) ? null : c.S,
                string.IsNullOrEmpty(c.A) ? null : c.A))];
    }

    private static string N(string? v) => string.IsNullOrWhiteSpace(v) ? "" : v.Trim();
    private static string Data(DateOnly? d) => d?.ToString("dd/MM/yyyy") ?? "";
    private static string Lista(IReadOnlyList<string>? l) => l is { Count: > 0 } ? string.Join(", ", l) : "";

    private static string End(EnderecoDto? e) => e is null ? "" :
        $"{N(e.Logradouro)}{(string.IsNullOrWhiteSpace(e.Numero) ? "" : ", " + e.Numero)}"
        + $"{(string.IsNullOrWhiteSpace(e.Complemento) ? "" : " - " + e.Complemento)}, {N(e.Bairro)}, "
        + $"{N(e.Cidade)}/{N(e.Uf)}{(string.IsNullOrWhiteSpace(e.Cep) ? "" : " CEP " + e.Cep)}";

    private static string Ctt(ContatoEmergenciaDto? c) => c is null ? "" :
        $"{N(c.Nome)}{(string.IsNullOrWhiteSpace(c.Parentesco) ? "" : " (" + c.Parentesco + ")")} {N(c.Telefone)}".Trim();
}

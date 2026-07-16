using Hl7.Fhir.Model;
using SMSMarica.Core.Common.Dtos;
using SMSMarica.Core.Common.Excecoes;
using SMSMarica.Core.Pacientes.Dtos;
using SMSMarica.Core.Pacientes.Fhir;

namespace SMSMarica.Core.Pacientes;

/// <summary>
/// Pacientes agora vivem APENAS no hub FHIR (Automais.Fhir). Este serviço é um
/// proxy: consultar/cadastrar/atualizar paciente vira chamada à API FHIR
/// (ADR-0010 + regra "FHIR é API-only; smsmarica é o consumidor").
/// </summary>
public sealed class PacientesService(
    IPacienteFhirClient fhir,
    Auditoria.IAuditoriaService auditoria,
    Geo.IGeocodificadorService geocoder) : IPacientesService
{
    private const int LimiteBusca = 10;

    public async Task<IReadOnlyList<PacienteListItemDto>> BuscarAsync(
        string? termo, CancellationToken cancellationToken = default)
    {
        Hl7.Fhir.Model.Bundle bundle;

        if (string.IsNullOrWhiteSpace(termo))
        {
            bundle = await fhir.BuscarAsync(ct: cancellationToken);
        }
        else
        {
            termo = termo.Trim();
            var digitos = Digitos(termo);
            var soDigitos = digitos.Length == termo.Replace(".", "").Replace("-", "").Replace(" ", "").Length;

            // Até 15 dígitos: cobre o CPF (11) e também o CNS (15) — os dois são identifier no hub.
            bundle = digitos.Length >= 3 && digitos.Length <= 15 && soDigitos
                ? await fhir.BuscarAsync(identifier: digitos, ct: cancellationToken)
                : await fhir.BuscarAsync(name: termo, ct: cancellationToken);
        }

        return [.. bundle.Entry
            .Select(e => e.Resource)
            .OfType<Hl7.Fhir.Model.Patient>()
            .Take(LimiteBusca)
            .Select(PacienteFhirMapper.ParaListItem)];
    }

    public async Task<PacienteDto> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var patient = await fhir.ObterAsync(id, cancellationToken)
            ?? throw new NaoEncontradoException("Paciente", id);
        return PacienteFhirMapper.ParaDto(patient);
    }

    public async Task<PacienteExistenciaDto?> ObterPorCpfAsync(string cpf, CancellationToken cancellationToken = default)
    {
        var normalizado = Digitos(cpf);
        if (normalizado.Length != 11) return null;

        var bundle = await fhir.BuscarAsync(identifier: normalizado, ct: cancellationToken);
        var patient = bundle.Entry.Select(e => e.Resource).OfType<Hl7.Fhir.Model.Patient>().FirstOrDefault();
        if (patient is null) return null;

        var dto = PacienteFhirMapper.ParaDto(patient);
        return new PacienteExistenciaDto(dto.Id, dto.NomeCompleto, dto.Cpf, dto.Ativo);
    }

    public async Task<PacienteExistenciaDto?> ObterPorCnsAsync(string cns, CancellationToken cancellationToken = default)
    {
        var normalizado = Digitos(cns);
        if (normalizado.Length != 15) return null;

        var bundle = await fhir.BuscarAsync(identifier: normalizado, ct: cancellationToken);
        var patient = bundle.Entry.Select(e => e.Resource).OfType<Patient>().FirstOrDefault();
        if (patient is null) return null;

        var dto = PacienteFhirMapper.ParaDto(patient);
        return new PacienteExistenciaDto(dto.Id, dto.NomeCompleto, dto.Cpf, dto.Ativo);
    }

    public async Task<PacienteExistenciaDto?> ObterPorTelefoneAsync(string telefone, CancellationToken cancellationToken = default)
    {
        var patient = (await BuscarPorTelefoneAsync(telefone, cancellationToken)).FirstOrDefault();
        if (patient is null) return null;

        var dto = PacienteFhirMapper.ParaDto(patient);
        return new PacienteExistenciaDto(dto.Id, dto.NomeCompleto, dto.Cpf, dto.Ativo);
    }

    public async Task<IReadOnlyList<PacienteListItemDto>> ListarPorTelefoneAsync(
        string telefone, CancellationToken cancellationToken = default) =>
        [.. (await BuscarPorTelefoneAsync(telefone, cancellationToken)).Select(PacienteFhirMapper.ParaListItem)];

    private async Task<IReadOnlyList<Patient>> BuscarPorTelefoneAsync(string telefone, CancellationToken ct)
    {
        var numero = Digitos(telefone);
        // O hub guarda os telefones na forma NACIONAL (DDD+número); o WhatsApp/canônico chega
        // com DDI 55 (12/13 díg.) — sem tirar o DDI, o Contains da busca nunca casa.
        if (numero.StartsWith("55", StringComparison.Ordinal) && numero.Length is 12 or 13)
            numero = numero[2..];
        if (numero.Length < 8) return [];

        var bundle = await fhir.BuscarAsync(telecom: numero, ct: ct);
        return [.. bundle.Entry.Select(e => e.Resource).OfType<Patient>()];
    }

    public async Task<Guid> CadastrarAsync(CadastrarPacienteRequest request, CancellationToken cancellationToken = default)
    {
        var cpf = Digitos(request.Cpf);
        // Sem CPF válido (11 dígitos) NÃO buscamos por identifier vazio: o hub trataria a
        // ausência de filtro como "listar todos" e um match qualquer viraria falso "CPF duplicado".
        if (cpf.Length != 11)
            throw new ValidacaoException("paciente.cpf_invalido",
                "CPF ausente ou inválido — não é possível cadastrar o paciente sem um CPF de 11 dígitos.");

        var existentes = await fhir.BuscarAsync(identifier: cpf, ct: cancellationToken);
        if (existentes.Entry.Select(e => e.Resource).OfType<Hl7.Fhir.Model.Patient>().Any())
            throw new ConflitoException("paciente.cpf_duplicado", "Já existe paciente com este CPF no hub FHIR.");

        var patient = PacienteFhirMapper.ConstruirNovo(request);
        var coord = await GeocodificarAsync(request.Endereco, cancellationToken);
        if (coord is not null) PatientMergeFhir.SetGeolocation(patient, coord.Latitude, coord.Longitude);

        var criado = await fhir.CriarAsync(patient, cancellationToken);
        return Guid.Parse(criado.Id!);
    }

    /// <summary>
    /// Geocodifica o endereço (cache → Google) para a geolocalização do Patient — mata o lat/long
    /// 0,0 (ADR-0020 R2). Best-effort: endereço que não resolve entra na fila de revisão e a
    /// operação NÃO falha por isso (retorna null).
    /// </summary>
    private async Task<Geo.Coordenada?> GeocodificarAsync(Common.Dtos.EnderecoDto? endereco, CancellationToken ct)
    {
        if (endereco is null) return null;
        try { return await geocoder.GeocodificarAsync(endereco.ParaEntidade(), ct); }
        catch { return null; }
    }

    public Task<Guid> PromoverAsync(PromoverPacienteRequest request, CancellationToken cancellationToken = default) =>
        throw new ConflitoException(
            "paciente.promover_descontinuado",
            "Promover usuário a paciente foi descontinuado: paciente é recurso do hub FHIR (use POST /pacientes).");

    public async Task AtualizarAsync(Guid id, AtualizarPacienteRequest request, CancellationToken cancellationToken = default)
    {
        // Snapshot ANTES para a trilha de auditoria (ticket #30: auditar telefone e demais campos,
        // não só o nome). Ler o DTO atual custa 1 leitura no hub — aceitável numa edição.
        var antes = await ObterPorIdAsync(id, cancellationToken);

        // Geocodifica uma vez (fora do retry); a coordenada é aplicada em cada tentativa.
        var coord = await GeocodificarAsync(request.Endereco, cancellationToken);
        await AtualizarComRetryAsync(id, patient =>
        {
            PacienteFhirMapper.AplicarAtualizacao(patient, request);
            if (coord is not null) PatientMergeFhir.SetGeolocation(patient, coord.Latitude, coord.Longitude);
            return true;
        }, cancellationToken);

        var depois = await ObterPorIdAsync(id, cancellationToken);
        await RegistrarAlteracoesAsync(id, antes, depois, cancellationToken);
    }

    /// <summary>
    /// Compara o paciente antes/depois da edição e grava uma entrada de auditoria por CAMPO
    /// alterado (ticket #30). Nome/CPF/nascimento são imutáveis aqui (nome tem trilha própria em
    /// <see cref="AtualizarNomeAsync"/>). null e vazio contam como iguais (não gera ruído).
    /// </summary>
    private async Task RegistrarAlteracoesAsync(
        Guid id, PacienteDto antes, PacienteDto depois, CancellationToken ct)
    {
        static string N(string? v) => string.IsNullOrWhiteSpace(v) ? "" : v.Trim();
        static string Lista(IReadOnlyList<string>? l) => l is { Count: > 0 } ? string.Join(", ", l) : "";
        static string End(EnderecoDto? e) => e is null ? "" :
            $"{N(e.Logradouro)}{(string.IsNullOrWhiteSpace(e.Numero) ? "" : ", " + e.Numero)}"
            + $"{(string.IsNullOrWhiteSpace(e.Complemento) ? "" : " - " + e.Complemento)}, {N(e.Bairro)}, "
            + $"{N(e.Cidade)}/{N(e.Uf)}{(string.IsNullOrWhiteSpace(e.Cep) ? "" : " CEP " + e.Cep)}";
        static string Ctt(ContatoEmergenciaDto? c) => c is null ? "" :
            $"{N(c.Nome)}{(string.IsNullOrWhiteSpace(c.Parentesco) ? "" : " (" + c.Parentesco + ")")} {N(c.Telefone)}".Trim();

        // (rótulo, valor-antes, valor-depois)
        var campos = new List<(string Rotulo, string Antes, string Depois)>
        {
            ("CNS", N(antes.Cns), N(depois.Cns)),
            ("RG", N(antes.Rg), N(depois.Rg)),
            ("Sexo", antes.Sexo.ToString(), depois.Sexo.ToString()),
            ("Estado civil", antes.EstadoCivil.ToString(), depois.EstadoCivil.ToString()),
            ("Raça/cor", antes.RacaCor.ToString(), depois.RacaCor.ToString()),
            ("Escolaridade", antes.Escolaridade.ToString(), depois.Escolaridade.ToString()),
            ("Ocupação", N(antes.Ocupacao), N(depois.Ocupacao)),
            ("Naturalidade", N(antes.Naturalidade), N(depois.Naturalidade)),
            ("Nacionalidade", N(antes.Nacionalidade), N(depois.Nacionalidade)),
            ("Nome da mãe", N(antes.NomeDaMae), N(depois.NomeDaMae)),
            ("Nome do pai", N(antes.NomeDoPai), N(depois.NomeDoPai)),
            ("Responsável legal", N(antes.ResponsavelLegal), N(depois.ResponsavelLegal)),
            ("Endereço", End(antes.Endereco), End(depois.Endereco)),
            ("Telefone principal", N(antes.TelefonePrincipal), N(depois.TelefonePrincipal)),
            ("Telefone celular", N(antes.TelefoneCelular), N(depois.TelefoneCelular)),
            ("Telefone residencial", N(antes.TelefoneResidencial), N(depois.TelefoneResidencial)),
            ("E-mail", N(antes.Email), N(depois.Email)),
            ("Contato de emergência", Ctt(antes.ContatoEmergencia), Ctt(depois.ContatoEmergencia)),
            ("Altura (cm)", antes.AlturaCm?.ToString() ?? "", depois.AlturaCm?.ToString() ?? ""),
            ("Peso (kg)", antes.PesoKg?.ToString() ?? "", depois.PesoKg?.ToString() ?? ""),
            ("Tipo sanguíneo", antes.TipoSanguineo.ToString(), depois.TipoSanguineo.ToString()),
            ("Fator Rh", antes.FatorRh.ToString(), depois.FatorRh.ToString()),
            ("Alergias", Lista(antes.Alergias), Lista(depois.Alergias)),
            ("Medicamentos contínuos", Lista(antes.MedicamentosContinuos), Lista(depois.MedicamentosContinuos)),
            ("Comorbidades", Lista(antes.Comorbidades), Lista(depois.Comorbidades)),
            ("Deficiências", Lista(antes.Deficiencias), Lista(depois.Deficiencias)),
            ("Plano de saúde", N(antes.PlanoSaude), N(depois.PlanoSaude)),
            ("Observações", N(antes.Observacoes), N(depois.Observacoes)),
            ("Nome social", N(antes.NomeSocial), N(depois.NomeSocial)),
        };

        foreach (var (rotulo, va, vd) in campos)
        {
            if (string.Equals(va, vd, StringComparison.Ordinal)) continue;
            await auditoria.RegistrarAsync(
                "Paciente", id.ToString(), $"Alteração de {rotulo}", va, vd, ct);
        }

        // Foto: não guardar o base64 (enorme) — só registrar que mudou.
        var fotoAntes = !string.IsNullOrWhiteSpace(antes.FotoBase64);
        var fotoDepois = !string.IsNullOrWhiteSpace(depois.FotoBase64);
        if (fotoAntes != fotoDepois || (fotoDepois && antes.FotoBase64 != depois.FotoBase64))
            await auditoria.RegistrarAsync(
                "Paciente", id.ToString(), "Alteração de foto",
                fotoAntes ? "(com foto)" : "(sem foto)",
                fotoDepois ? "(com foto)" : "(sem foto)", ct);
    }

    public async Task AtualizarNomeAsync(Guid id, AtualizarNomePacienteRequest request, CancellationToken cancellationToken = default)
    {
        var nomeNovo = request.NomeCompleto.Trim();
        string? nomeAnterior = null;
        var alterou = false;

        await AtualizarComRetryAsync(id, patient =>
        {
            nomeAnterior = PacienteFhirMapper.NomeDe(patient);
            if (string.Equals(nomeAnterior, nomeNovo, StringComparison.Ordinal)) return false;
            PacienteFhirMapper.AplicarNome(patient, nomeNovo);
            alterou = true;
            return true;
        }, cancellationToken);

        if (alterou)
            await auditoria.RegistrarAsync(
                "Paciente", id.ToString(), "AlteracaoNome", nomeAnterior, nomeNovo, cancellationToken);
    }

    public async Task AdicionarTelefoneAsync(
        Guid id, AdicionarTelefoneRequest request, CancellationToken cancellationToken = default)
    {
        var numero = request.Numero?.Trim() ?? string.Empty;
        if (numero.Length == 0)
            throw new ValidacaoException("paciente.telefone_obrigatorio", "Informe o número do telefone.");

        await AtualizarComRetryAsync(id, patient =>
        {
            // Append em Patient.telecom nativo, sem tocar nos demais dados. Idempotente:
            // se o mesmo número (comparando só dígitos) já estiver lá, não duplica.
            var alvo = Digitos(numero);
            patient.Telecom ??= [];
            var jaExiste = alvo.Length > 0 && patient.Telecom.Any(t =>
                t.System == ContactPoint.ContactPointSystem.Phone && Digitos(t.Value) == alvo);
            if (jaExiste) return false;

            patient.Telecom.Add(new ContactPoint
            {
                System = ContactPoint.ContactPointSystem.Phone,
                Value = numero,
                Use = MapearUso(request.Tipo),
            });
            return true;
        }, cancellationToken);
    }

    /// <summary>
    /// Read-modify-write com concorrência otimista: lê o Patient, aplica <paramref name="mutar"/> e grava
    /// com If-Match; em conflito (edição concorrente) re-lê e reaplica, até <paramref name="maxTentativas"/>.
    /// <paramref name="mutar"/> devolve <c>false</c> para no-op (não grava).
    /// </summary>
    private async Task AtualizarComRetryAsync(Guid id, Func<Patient, bool> mutar,
        CancellationToken ct, int maxTentativas = 3)
    {
        for (var tentativa = 1; ; tentativa++)
        {
            var patient = await fhir.ObterAsync(id, ct)
                ?? throw new NaoEncontradoException("Paciente", id);
            if (!mutar(patient)) return;
            try
            {
                await fhir.AtualizarAsync(id, patient, ct);
                return;
            }
            catch (Pacientes.Fhir.ConflitoVersaoHubException) when (tentativa < maxTentativas)
            {
                // Alguém alterou o paciente entre o GET e o PUT — re-lê e reaplica.
            }
        }
    }

    public async Task AtualizarFotoAsync(Guid id, string? fotoBase64, CancellationToken cancellationToken = default)
    {
        var atual = await ObterPorIdAsync(id, cancellationToken);
        var req = RequestCompletoDeDto(atual) with { FotoBase64 = string.IsNullOrWhiteSpace(fotoBase64) ? null : fotoBase64 };
        await AtualizarAsync(id, req, cancellationToken);
    }

    public async Task AtualizarContatoAsync(
        Guid id, string? email, string? telefonePrincipal, string? telefoneCelular,
        string? telefoneResidencial, CancellationToken cancellationToken = default)
    {
        var atual = await ObterPorIdAsync(id, cancellationToken);
        var req = RequestCompletoDeDto(atual) with
        {
            Email = email,
            TelefonePrincipal = telefonePrincipal,
            TelefoneCelular = telefoneCelular,
            TelefoneResidencial = telefoneResidencial,
        };
        await AtualizarAsync(id, req, cancellationToken);
    }

    /// <summary>
    /// Reconstrói um <see cref="AtualizarPacienteRequest"/> com TODOS os campos do estado
    /// atual. Como <see cref="PacienteFhirMapper.AplicarAtualizacao"/> regrava o payload a
    /// partir do request, edições parciais devem partir do estado completo para não apagar dados.
    /// </summary>
    private static AtualizarPacienteRequest RequestCompletoDeDto(PacienteDto p) => new(
        Cns: p.Cns,
        Rg: p.Rg,
        Sexo: p.Sexo,
        EstadoCivil: p.EstadoCivil,
        RacaCor: p.RacaCor,
        Escolaridade: p.Escolaridade,
        Ocupacao: p.Ocupacao,
        Naturalidade: p.Naturalidade,
        Nacionalidade: p.Nacionalidade,
        NomeDaMae: p.NomeDaMae,
        NomeDoPai: p.NomeDoPai,
        ResponsavelLegal: p.ResponsavelLegal,
        Endereco: p.Endereco,
        TelefonePrincipal: p.TelefonePrincipal,
        TelefoneCelular: p.TelefoneCelular,
        TelefoneResidencial: p.TelefoneResidencial,
        Email: p.Email,
        ContatoEmergencia: p.ContatoEmergencia,
        AlturaCm: p.AlturaCm,
        PesoKg: p.PesoKg,
        TipoSanguineo: p.TipoSanguineo,
        FatorRh: p.FatorRh,
        Alergias: p.Alergias,
        MedicamentosContinuos: p.MedicamentosContinuos,
        Comorbidades: p.Comorbidades,
        Deficiencias: p.Deficiencias,
        PlanoSaude: p.PlanoSaude,
        Observacoes: p.Observacoes,
        FotoBase64: p.FotoBase64,
        NomeSocial: p.NomeSocial);

    private static ContactPoint.ContactPointUse MapearUso(string? tipo) => tipo?.Trim().ToLowerInvariant() switch
    {
        "residencial" or "casa" or "home" => ContactPoint.ContactPointUse.Home,
        "comercial" or "trabalho" or "work" => ContactPoint.ContactPointUse.Work,
        _ => ContactPoint.ContactPointUse.Mobile,
    };

    public Task DesativarAsync(Guid id, CancellationToken cancellationToken = default) =>
        fhir.ExcluirAsync(id, cancellationToken);

    public Task ReativarAsync(Guid id, CancellationToken cancellationToken = default) =>
        throw new ConflitoException(
            "paciente.reativar_nao_suportado",
            "Reativação não está disponível nesta fase (o hub FHIR ainda não expõe undelete).");

    private static string Digitos(string? valor) =>
        string.IsNullOrEmpty(valor) ? string.Empty : new([.. valor.Where(char.IsDigit)]);
}

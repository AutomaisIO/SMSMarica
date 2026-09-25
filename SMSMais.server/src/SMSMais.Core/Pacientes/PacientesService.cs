using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using SMSMais.Core.Common.Dtos;
using SMSMais.Core.Common.Documentos;
using SMSMais.Core.Common.Excecoes;
using SMSMais.Core.Pacientes.Dtos;
using SMSMais.Core.Pacientes.Fhir;

namespace SMSMais.Core.Pacientes;

/// <summary>
/// Pacientes agora vivem APENAS no hub FHIR (Automais.Fhir). Este serviço é um
/// proxy: consultar/cadastrar/atualizar paciente vira chamada à API FHIR
/// (ADR-0010 + regra "FHIR é API-only; smsmarica é o consumidor").
/// </summary>
public sealed class PacientesService(
    IPacienteFhirClient fhir,
    Auditoria.IAuditoriaService auditoria,
    Geo.IGeocodificadorService geocoder,
    Microsoft.Extensions.Logging.ILogger<PacientesService> logger) : IPacientesService
{
    private const int LimiteBusca = 10;

    public async Task<IReadOnlyList<PacienteListItemDto>> BuscarAsync(
        string? termo, CancellationToken cancellationToken = default)
    {
        Hl7.Fhir.Model.Bundle bundle;

        try
        {
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
                // O CNS só chega lá QUALIFICADO: sem o system o hub compara o número com a coluna de
                // CPF e devolve vazio, que era o motivo de procurar paciente pelo CNS não achar nada.
                var chave = digitos.Length switch
                {
                    11 => $"{PatientMergeFhir.SystemCpf}|{digitos}",
                    15 => $"{PatientMergeFhir.SystemCns}|{digitos}",
                    _ => digitos,
                };
                bundle = digitos.Length >= 3 && digitos.Length <= 15 && soDigitos
                    ? await fhir.BuscarAsync(identifier: chave, ct: cancellationToken)
                    : await fhir.BuscarAsync(name: termo, ct: cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            // Barra de busca-enquanto-digita: hub indisponível/lento degrada para lista vazia em
            // vez de 500 (era o ERRO-C3NQQ2). O timeout do HttpClient lança TaskCanceledException
            // com o ct do chamador intacto — por isso o `|| !cancellationToken.IsCancellationRequested`.
            // Só a cancelação do CHAMADOR (usuário mudou o termo/fechou a tela) propaga.
            logger.LogWarning(ex, "Busca de pacientes no hub FHIR falhou — devolvendo lista vazia.");
            return [];
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

        // COM o system, sempre. O hub trata `identifier` sem `|` como CPF (SepararIdentifier), então
        // um CNS pelado virava `WHERE cpf = '<15 dígitos>'` — que não casa nunca, e devolvia "não
        // existe" para paciente que existe. Silencioso e caro: esta é a ÚNICA verificação de "já
        // temos este paciente" do importador do SISREG, e sem ela toda linha ia ao CADSUS.
        // Com o system, cai no ramo que procura em `cns_todos` pelo índice GIN — que já estava
        // escrito no hub e era inalcançável.
        var bundle = await fhir.BuscarAsync(
            identifier: $"{PatientMergeFhir.SystemCns}|{normalizado}", ct: cancellationToken);
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

        try
        {
            var bundle = await fhir.BuscarAsync(telecom: numero, ct: ct);
            return [.. bundle.Entry.Select(e => e.Resource).OfType<Patient>()];
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Caminho de EXIBIÇÃO (quais pacientes têm este telefone, na thread da conversa):
            // hub indisponível/lento degrada para vazio em vez de virar 500. O timeout do
            // HttpClient lança TaskCanceledException com o ct do chamador intacto — por isso o
            // `|| !ct.IsCancellationRequested`. NÃO é o guard de unicidade do OTP (esse fica em
            // TelefoneValidacaoService e não pode degradar para "número livre").
            logger.LogWarning(ex, "Busca de pacientes por telefone no hub FHIR falhou — seguindo sem resultados.");
            return [];
        }
    }

    public async Task<Guid> CadastrarAsync(CadastrarPacienteRequest request, CancellationToken cancellationToken = default)
    {
        var cpf = CpfBr.SoDigitos(request.Cpf);
        var cns = Digitos(request.Cns);

        // CPF preenchido tem de fechar o DV. "11 dígitos" não basta: 00000000000 passaria e
        // viraria chave nacional de duas pessoas diferentes (adendo do ADR-0041).
        if (cpf.Length > 0 && !CpfBr.EhValido(cpf))
            throw new ValidacaoException("paciente.cpf_invalido", "CPF inválido — confira os dígitos.");

        // Precisa de ALGUMA chave nacional. Sem CPF e sem CNS não há como afirmar quem é a pessoa,
        // e dedup por nome+nascimento é o caminho curto para fundir dois pacientes (ADR-0041).
        if (cpf.Length == 0 && cns.Length != 15)
        {
            throw new ValidacaoException(
                "paciente.sem_chave_nacional",
                "Sem CPF e sem CNS não é possível cadastrar o paciente com segurança — não haveria "
                + "como distinguir esta pessoa de um homônimo.");
        }

        // A busca é SEMPRE por uma chave preenchida: com identifier vazio o hub trataria a
        // ausência de filtro como "listar todos", e um match qualquer viraria falso "duplicado".
        //
        // E SEMPRE com o system. Sem ele o hub lê o valor como CPF, então a checagem por CNS —
        // o caminho de quem não tem CPF (ADR-0041) — não encontrava nunca: `cns_duplicado` era
        // inalcançável e cada cadastro repetido criava uma pessoa nova no hub.
        var chave = cpf.Length == 11
            ? $"{PatientMergeFhir.SystemCpf}|{cpf}"
            : $"{PatientMergeFhir.SystemCns}|{cns}";
        var existentes = await fhir.BuscarAsync(identifier: chave, ct: cancellationToken);
        if (existentes.Entry.Select(e => e.Resource).OfType<Hl7.Fhir.Model.Patient>().Any())
        {
            throw cpf.Length == 11
                ? new ConflitoException("paciente.cpf_duplicado", "Já existe paciente com este CPF no hub FHIR.")
                : new ConflitoException("paciente.cns_duplicado", "Já existe paciente com este CNS no hub FHIR.");
        }

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

    public async Task DefinirCpfAsync(Guid id, string cpf, CancellationToken cancellationToken = default)
    {
        var digitos = CpfBr.SoDigitos(cpf);
        if (!CpfBr.EhValido(digitos))
        {
            throw new ValidacaoException(
                "paciente.cpf_invalido",
                "CPF inválido — confira os dígitos. (A checagem é do dígito verificador: um número "
                + "com 11 dígitos que não fecha não identifica ninguém e fundiria cadastros.)");
        }

        string? anterior = null;
        var alterou = false;

        await AtualizarComRetryAsync(id, patient =>
        {
            anterior = PacienteFhirMapper.CpfDe(patient);

            // Já tem CPF e é OUTRO: não é correção de digitação, é troca de identidade. Recusa.
            if (!string.IsNullOrWhiteSpace(anterior) && anterior != digitos)
            {
                throw new ConflitoException(
                    "paciente.cpf_ja_definido",
                    $"Este paciente já está cadastrado com o CPF {Mascarar(anterior)}. Trocar o CPF "
                    + "de um cadastro existente muda a identidade da pessoa — se for outra pessoa, "
                    + "use o cadastro dela.");
            }

            if (anterior == digitos) return false; // idempotente

            PacienteFhirMapper.AplicarCpf(patient, digitos);
            alterou = true;
            return true;
        }, cancellationToken);

        if (alterou)
        {
            await auditoria.RegistrarAsync(
                "Paciente", id.ToString(), "DefinicaoCpf", "", digitos, cancellationToken);
        }
    }

    public async Task AbsorverIdentificadoresAsync(
        Guid destinoId, string? cns, string? telefone, CancellationToken cancellationToken = default)
    {
        var cnsLimpo = Digitos(cns);
        if (cnsLimpo.Length == 15)
        {
            await AtualizarComRetryAsync(destinoId, patient =>
            {
                // Se o destino já tem ESTE CNS, nada a fazer. Se tem OUTRO, também não mexemos:
                // sobrescrever identificador nacional é o caminho para fundir pessoas erradas.
                var atual = PacienteFhirMapper.CnsDe(patient);
                if (!string.IsNullOrWhiteSpace(atual)) return false;
                PacienteFhirMapper.AplicarCns(patient, cnsLimpo);
                return true;
            }, cancellationToken);

            await auditoria.RegistrarAsync(
                "Paciente", destinoId.ToString(), "AbsorcaoCns", "", cnsLimpo, cancellationToken);
        }

        // Append, nunca substituição — o principal é o contato validado por OTP e é intocável.
        if (!string.IsNullOrWhiteSpace(telefone))
            await AdicionarTelefoneAsync(destinoId, new AdicionarTelefoneRequest(telefone), cancellationToken);
    }

    private static string Mascarar(string cpf) =>
        cpf.Length == 11 ? $"***.***.{cpf[6..9]}-{cpf[9..]}" : "***";

    public async Task<bool> AdicionarTelefoneAsync(
        Guid id, AdicionarTelefoneRequest request, CancellationToken cancellationToken = default)
    {
        var numero = request.Numero?.Trim() ?? string.Empty;
        if (numero.Length == 0)
            throw new ValidacaoException("paciente.telefone_obrigatorio", "Informe o número do telefone.");

        var origem = string.IsNullOrWhiteSpace(request.Origem) ? null : request.Origem.Trim().ToLowerInvariant();
        var adicionou = false;
        await AtualizarComRetryAsync(id, patient =>
        {
            // Append em Patient.telecom nativo, sem tocar nos demais dados. Idempotente:
            // se o mesmo número (só dígitos, tolerando DDI) já estiver lá — inclusive aposentado
            // ou negado —, não duplica nem ressuscita.
            var alvo = Digitos(numero);
            patient.Telecom ??= [];
            var jaExiste = alvo.Length > 0 && patient.Telecom.Any(t =>
                t.System == ContactPoint.ContactPointSystem.Phone && MesmoNumero(Digitos(t.Value), alvo));
            if (jaExiste) return adicionou = false;

            var novo = new ContactPoint
            {
                System = ContactPoint.ContactPointSystem.Phone,
                Value = numero,
                Use = MapearUso(request.Tipo),
            };
            if (origem is not null)
            {
                novo.AddExtension(PatientMergeFhir.ExtContatoOrigem, new FhirString(origem));
                novo.Period = new Period { StartElement = new FhirDateTime(DateTimeOffset.UtcNow) };
            }
            patient.Telecom.Add(novo);
            return adicionou = true;
        }, cancellationToken);
        return adicionou;
    }

    /// <summary>Mesmo número tolerando DDI (um é sufixo do outro), com guarda de tamanho.</summary>
    private static bool MesmoNumero(string a, string b) =>
        a == b
        || (a.Length >= 8 && b.Length >= 8
            && (a.EndsWith(b, StringComparison.Ordinal) || b.EndsWith(a, StringComparison.Ordinal)));

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

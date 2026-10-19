import 'dart:io';
import 'dart:typed_data';

import 'package:agente/features/auth/application/entrada_motorista_service.dart';
import 'package:agente/features/auth/data/fila_conferencias.dart';
import 'package:agente/features/auth/data/motorista_repository.dart';
import 'package:agente/features/auth/domain/entrada_motorista.dart';
import 'package:agente/features/auth/facial/data/armazenamento_facial.dart';
import 'package:agente/features/auth/facial/data/servico_facial.dart';
import 'package:agente/shared/auth/sessao_controller.dart';
import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:image/image.dart' as img;

/// Servidor de mentira: escala, fotos de referência e cadastro do rosto.
class _RepoFalso extends MotoristaRepository {
  _RepoFalso() : super(Dio());

  List<MotoristaEscalado> escala = [];
  bool semRede = false;
  bool recusarCadastro = false;
  final List<String> fotosBaixadas = [];
  final List<String> rostosEnviados = [];

  @override
  Future<List<MotoristaEscalado>> escaladosDoDia(String token) async {
    if (semRede) throw DioException(requestOptions: RequestOptions());
    return escala;
  }

  @override
  Future<({String fotoBase64, String fotoHash})> referencia(
    String token,
    String motoristaId,
  ) async {
    fotosBaixadas.add(motoristaId);
    final m = escala.firstWhere((e) => e.motoristaId == motoristaId);
    return (fotoBase64: 'AAAA', fotoHash: m.fotoHash!);
  }

  @override
  Future<String> cadastrarRosto(
    String token, {
    required String ticket,
    required String fotoBase64,
    required String termoVersao,
    double? latitude,
    double? longitude,
  }) async {
    if (recusarCadastro) throw const RecusaDaCentral('O termo mudou.');
    rostosEnviados.add(fotoBase64);
    return 'hash-servidor';
  }
}

/// Reconhecimento de mentira: guarda "vetores" por motorista e devolve o
/// resultado que o teste mandar.
class _FacialFalso implements ReconhecedorFacial {
  final Map<String, MotoristaFacial> registros = {};
  bool rostoNaFoto = true;
  ResultadoVerificacao resultado = const ResultadoVerificacao(
    aprovado: true,
    score: 0.9,
    motivo: 'ok',
    motoristaId: 'm1',
  );
  List<String>? candidatos;

  @override
  bool get matchDisponivel => true;

  @override
  String get versaoModelo => 'mobilefacenet-v1';

  @override
  Future<void> carregar() async {}

  @override
  Future<MotoristaFacial?> registro(String id) async => registros[id];

  @override
  Future<List<String>> cadastrados() async => registros.keys.toList();

  @override
  Future<void> esquecer(String id) async => registros.remove(id);

  @override
  Future<void> trocarHash(String id, String fotoHash) async {
    final m = registros[id]!;
    registros[id] = _registro(id, fotoHash, m.modeloVersao);
  }

  @override
  Future<bool> cadastrar({
    required String motoristaId,
    required String cpf,
    required String nome,
    required String caminhoFoto,
    required String fotoHash,
  }) async {
    if (!rostoNaFoto) return false;
    registros[motoristaId] = _registro(motoristaId, fotoHash, versaoModelo);
    return true;
  }

  @override
  Future<ResultadoVerificacao> verificar({
    required String caminhoFoto,
    String? motoristaId,
    Iterable<String>? candidatos,
  }) async {
    this.candidatos = candidatos?.toList();
    return resultado;
  }

  static MotoristaFacial _registro(String id, String hash, String versao) =>
      MotoristaFacial(
        motoristaId: id,
        cpf: '',
        nome: id,
        embedding: Float32List(4),
        fotoHash: hash,
        modeloVersao: versao,
        atualizadoEm: DateTime(2026, 10, 15),
      );
}

MotoristaEscalado _escalado(String id, {String? hash}) => MotoristaEscalado(
  motoristaId: id,
  nome: 'Motorista $id',
  rotaId: 'rota-$id',
  temReferencia: hash != null,
  fotoHash: hash,
);

/// Uma foto de verdade em disco (o serviço reduz o JPEG antes de usar).
Future<String> _fotoEmDisco() async {
  final dir = await Directory.systemTemp.createTemp('teste_rosto');
  addTearDown(() => dir.delete(recursive: true));
  final f = File('${dir.path}/foto.jpg');
  await f.writeAsBytes(img.encodeJpg(img.Image(width: 1280, height: 720)));
  return f.path;
}

void main() {
  late _RepoFalso repo;
  late _FacialFalso facial;
  late FilaConferenciasMemoria fila;
  late EntradaMotoristaService servico;
  var agora = DateTime.utc(2026, 10, 15, 13);

  EntradaMotoristaService novoServico() => EntradaMotoristaService(
    repo: repo,
    facial: facial,
    escala: EscalaLocal(const FlutterSecureStorage(), relogio: () => agora),
    fila: fila,
  );

  setUp(() {
    FlutterSecureStorage.setMockInitialValues({});
    agora = DateTime.utc(2026, 10, 15, 13);
    repo = _RepoFalso();
    facial = _FacialFalso();
    fila = FilaConferenciasMemoria();
    servico = novoServico();
  });

  group('escala do dia', () {
    test('baixa só a foto que mudou e esquece quem saiu da escala', () async {
      facial.registros['m1'] = _FacialFalso._registro(
        'm1',
        'h1',
        'mobilefacenet-v1',
      );
      facial.registros['saiu'] = _FacialFalso._registro(
        'saiu',
        'x',
        'mobilefacenet-v1',
      );
      repo.escala = [
        _escalado('m1', hash: 'h1'),
        _escalado('m2', hash: 'h2'),
        _escalado('m3'),
      ];

      final escala = await servico.carregarEscala('t');

      expect(escala.atualizada, isTrue);
      expect(repo.fotosBaixadas, ['m2']);
      expect(facial.registros.keys, unorderedEquals(['m1', 'm2']));
      expect(escala.algumComRosto, isTrue);
      expect(escala.algumSemRosto, isTrue);
    });

    test('sem rede usa a cópia de hoje; a de ontem não vale', () async {
      repo.escala = [_escalado('m1', hash: 'h1')];
      await servico.carregarEscala('t');
      repo.semRede = true;

      final hoje = await servico.carregarEscala('t');
      expect(hoje.atualizada, isFalse);
      expect(hoje.motoristas.single.motoristaId, 'm1');

      // 03:30 UTC do dia 16 = 00:30 em Brasília: outro dia.
      agora = DateTime.utc(2026, 10, 16, 3, 30);
      final amanha = await novoServico().carregarEscala('t');
      expect(amanha.motoristas, isEmpty);
    });
  });

  group('conferência pelo rosto', () {
    test('compara só com os escalados com rosto e põe na fila', () async {
      repo.escala = [_escalado('m1', hash: 'h1'), _escalado('m2')];
      final escala = await servico.carregarEscala('t');

      final r = await servico.conferirRosto(
        escala: escala,
        caminhoFoto: await _fotoEmDisco(),
        momento: MomentoConferencia.login,
        posicao: (latitude: -22.9, longitude: -42.8, precisaoM: 6),
      );

      expect(r.aprovado, isTrue);
      expect(facial.candidatos, ['m1']);
      final c = (await fila.pendentes()).single;
      expect(c.motoristaId, 'm1');
      expect(c.rotaId, 'rota-m1');
      expect(c.momento, MomentoConferencia.login);
      expect(c.nota, 0.9);
      expect(c.selfieBase64, isNotNull);
      expect(c.latitude, -22.9);
    });

    test('reprovada também vai para a fila', () async {
      facial.resultado = const ResultadoVerificacao(
        aprovado: false,
        score: 0.31,
        motivo: 'Rosto não confere',
        motoristaId: 'm1',
      );
      await servico.conferirRosto(
        escala: const EscalaDoDia(motoristas: [], atualizada: false),
        caminhoFoto: await _fotoEmDisco(),
        momento: MomentoConferencia.inicioTranslado,
        motoristaId: 'm1',
      );

      final c = (await fila.pendentes()).single;
      expect(c.aprovada, isFalse);
      expect(c.nota, 0.31);
      expect(facial.candidatos, ['m1']);
    });
  });

  group('primeiro acesso', () {
    const liberado = PrimeiroAcessoLiberado(
      ticket: 'tk',
      motoristaId: 'm9',
      nome: 'Novo',
      termoVersao: '2026-10-15',
      termoTexto: 'termo',
    );

    test('manda a foto reduzida e guarda o hash do servidor', () async {
      await servico.concluirPrimeiroAcesso('t', liberado, await _fotoEmDisco());

      expect(repo.rostosEnviados, hasLength(1));
      expect(facial.registros['m9']!.fotoHash, 'hash-servidor');
    });

    test('foto sem rosto não sai do tablet', () async {
      facial.rostoNaFoto = false;

      await expectLater(
        servico.concluirPrimeiroAcesso('t', liberado, await _fotoEmDisco()),
        throwsA(isA<SemRostoNaFoto>()),
      );
      expect(repo.rostosEnviados, isEmpty);
    });

    test('servidor recusou: o vetor sai do tablet', () async {
      repo.recusarCadastro = true;

      await expectLater(
        servico.concluirPrimeiroAcesso('t', liberado, await _fotoEmDisco()),
        throwsA(isA<RecusaDaCentral>()),
      );
      expect(facial.registros, isEmpty);
    });
  });

  group('sessão do motorista', () {
    test('vale até a virada do dia em Brasília', () {
      final s = Sessao(
        motoristaId: 'm1',
        nome: 'A',
        meio: MeioEntrada.rosto,
        entrouEm: DateTime.utc(2026, 10, 15, 22), // 19h em Brasília
      );
      expect(s.valeEm(DateTime.utc(2026, 10, 16, 2, 59)), isTrue);
      expect(s.valeEm(DateTime.utc(2026, 10, 16, 3, 1)), isFalse);
    });

    test('volta depois de reiniciar o app no mesmo dia', () async {
      const storage = FlutterSecureStorage();
      final antes = SessaoController(storage, relogio: () => agora);
      await antes.entrar(
        Sessao(
          motoristaId: 'm1',
          nome: 'A',
          meio: MeioEntrada.codigoCentral,
          entrouEm: agora,
        ),
      );

      final depois = SessaoController(storage, relogio: () => agora);
      await depois.carregar();
      expect(depois.state?.motoristaId, 'm1');
      expect(depois.state?.meio, MeioEntrada.codigoCentral);

      agora = agora.add(const Duration(days: 1));
      final outroDia = SessaoController(storage, relogio: () => agora);
      await outroDia.carregar();
      expect(outroDia.state, isNull);
    });
  });

  test('mensagem do servidor: validação e ProblemDetails', () {
    expect(
      MotoristaRepository.mensagemDoServidor({
        'errors': {
          'codigo.invalido': ['Código errado.'],
        },
      }),
      'Código errado.',
    );
    expect(
      MotoristaRepository.mensagemDoServidor({'detail': 'Já cadastrado.'}),
      'Já cadastrado.',
    );
    expect(MotoristaRepository.mensagemDoServidor('x'), contains('central'));
  });
}

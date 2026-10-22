import 'dart:async';

import 'package:agente/features/auth/application/entrada_motorista_service.dart';
import 'package:agente/features/auth/domain/entrada_motorista.dart';
import 'package:agente/features/auth/facial/presentation/captura_facial_page.dart';
import 'package:agente/shared/auth/sessao_controller.dart';
import 'package:agente/shared/gps/gps_ao_vivo.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

/// Antes de abrir a navegação de um translado, confere de novo quem está ao
/// volante (ADR-0073 §7) — no próprio tablet, sem precisar de rede. Quem
/// entrou pelo código da central e não tem rosto no tablet passa direto.
Future<bool> confirmarMotoristaParaTranslado(
  BuildContext context,
  WidgetRef ref,
) async {
  final sessao = ref.read(sessaoProvider);
  if (sessao == null) return false;
  final facial = ref.read(reconhecedorFacialProvider);
  await facial.carregar();
  final semRosto =
      !facial.matchDisponivel ||
      await facial.registro(sessao.motoristaId) == null;
  if (semRosto) return true;
  if (!context.mounted) return false;

  final captura = await Navigator.of(context).push<CapturaFacialResultado>(
    MaterialPageRoute(
      builder: (_) => CapturaFacialPage(
        finalidade: FinalidadeCaptura.inicioTranslado,
        nomeMotorista: sessao.nome,
      ),
    ),
  );
  if (captura == null) return false;

  final servico = ref.read(entradaMotoristaServiceProvider);
  final fix = ref.read(gpsAoVivoProvider);
  final r = await servico.conferirRosto(
    escala: await servico.escalaGuardada(),
    caminhoFoto: captura.caminhoFoto,
    momento: MomentoConferencia.inicioTranslado,
    motoristaId: sessao.motoristaId,
    posicao: fix == null
        ? null
        : (
            latitude: fix.latitude,
            longitude: fix.longitude,
            precisaoM: fix.accuracy > 0 ? fix.accuracy : null,
          ),
  );
  unawaited(ref.read(conferenciasProvider.notifier).enviarPendentes());
  if (r.aprovado) return true;
  if (context.mounted) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(
          'Não reconheci ${sessao.nome}. Tente de novo, de frente e com boa '
          'luz, ou toque em Sair para trocar de motorista.',
        ),
      ),
    );
  }
  return false;
}

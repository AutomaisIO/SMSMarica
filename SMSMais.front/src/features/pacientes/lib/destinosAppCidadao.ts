/**
 * Telas do app do cidadão que uma notificação pode abrir. Lista FIXA, a mesma do servidor (que
 * recusa o que estiver fora) e do app (que só navega para o que estiver dentro): mudou aqui,
 * muda nos três.
 */
export const DESTINOS_APP_CIDADAO: { rota: string; rotulo: string }[] = [
  { rota: '/', rotulo: 'Início' },
  { rota: '/agendados/consultas', rotulo: 'Consultas agendadas' },
  { rota: '/agendados/exames', rotulo: 'Exames agendados' },
  { rota: '/atendimentos', rotulo: 'Meus atendimentos' },
  { rota: '/exames', rotulo: 'Exames' },
  { rota: '/documentos', rotulo: 'Documentos' },
  { rota: '/transporte', rotulo: 'Transporte (TFD)' },
  { rota: '/chat', rotulo: 'Chat' },
  { rota: '/perfil', rotulo: 'Meu perfil' },
];

/** Sem rota, o toque abre o app no Início. */
export function rotuloDestinoApp(rota: string | null): string {
  if (!rota) return 'Início';
  return DESTINOS_APP_CIDADAO.find((d) => d.rota === rota)?.rotulo ?? rota;
}

export const ROTULO_PLATAFORMA: Record<string, string> = {
  android: 'Android',
  ios: 'iPhone',
};

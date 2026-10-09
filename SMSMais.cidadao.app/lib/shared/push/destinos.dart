/// Telas que uma notificação pode abrir — a MESMA lista fixa do servidor (que valida no envio) e do
/// painel (o select "abrir em"). O app confere de novo antes de navegar: o `data` do push vem de
/// fora do app e não pode levar a uma rota qualquer.
const destinosDoPush = {
  '/',
  '/agendados/consultas',
  '/agendados/exames',
  '/atendimentos',
  '/exames',
  '/documentos',
  '/transporte',
  '/chat',
  '/perfil',
};

/// A rota a abrir pelo toque na notificação, ou `null` se ela não estiver na lista fixa.
String? rotaDoPush(String? rota) => destinosDoPush.contains(rota) ? rota : null;

/// Para onde o toque leva. Sem `rota` = Início: é o "Início" padrão do painel, que não manda rota
/// (com o app em segundo plano noutra tela, ficar onde estava contradiria o que a equipe escolheu).
/// Rota fora da lista = `null`: o app só abre, sem navegar.
String? destinoDoToque(String? rota) => rota == null ? '/' : rotaDoPush(rota);

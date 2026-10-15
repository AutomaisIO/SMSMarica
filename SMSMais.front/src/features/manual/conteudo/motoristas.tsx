import { Truck } from 'lucide-react';
import { Callout } from '@/features/manual/components/Callout';
import { ListaDefinicoes, P, Sub } from '@/features/manual/components/Prosa';
import { Passos } from '@/features/manual/components/Passos';
import { AbaRef, BotaoRef, SeloRef } from '@/features/manual/components/Referencia';
import type { Artigo } from '@/features/manual/tipos';

/**
 * Artigo do cadastro de Motoristas do Transporte de Pacientes.
 *
 * Conferido no código: `features/motoristas` (MotoristasPage, MotoristaDetalhePage,
 * FormularioMotorista — passo do CPF + nascimento, promoção de usuário existente, abas Dados
 * pessoais / Motorista / Permissões), `shared/ui/SegurancaSecao`, `features/translados`
 * (TransladoFormPage filtra motorista ativo), `features/tratamentos/components/PainelConfirmacao` e
 * no backend (`MotoristasService` — CNH única, papel único, exclusão lógica que leva a conta junto,
 * categoria da CNH e regime CLT/RPA opcionais (`CategoriasCnh` no validador) —,
 * `MotoristasController`, `GeradorDeTransladoService`).
 * Reconhecimento facial no tablet (ADR-0073): `features/biometria` (ReconhecimentoFacialMotorista,
 * ConferenciasFaciais, LiberarEntradaMotorista), o app do tablet (`SMSMais.agente.app`,
 * features/auth — login_page: "Entrar pelo rosto", "Primeiro acesso (cadastrar meu rosto)", "Tenho
 * um código da central", aviso para pedir código depois de 3 rostos não reconhecidos) e o backend
 * (`BiometriaMotoristaController`, `BiometriaPainelService`, `BiometriaTabletService` — só o
 * escalado do dia, código no WhatsApp de 5 min, código da central de 15 min; `LimpezaSelfiesWorker`
 * — selfie sai depois de 90 dias; `MotoristasService` — excluir o motorista apaga o rosto).
 */
export const artigoMotoristas: Artigo = {
  slug: 'motoristas',
  titulo: 'Motoristas',
  resumo:
    'O cadastro de quem dirige no transporte de pacientes: dados conferidos pelo CPF, CNH, a conta de acesso, o rosto para entrar no tablet do veículo e onde o motorista aparece nas rotas.',
  grupo: 'transporte',
  icone: Truck,
  rota: '/app/motoristas',
  publico: 'Quem organiza o transporte de pacientes e cadastra a equipe',
  atualizadoEm: '2026-10-15',
  palavrasChave: [
    'motorista',
    'agente de transporte sanitário',
    'condutor',
    'CNH',
    'categoria da CNH',
    'habilitação',
    'regime de contratação',
    'CLT',
    'RPA',
    'CPF',
    'data de nascimento',
    'cadastrar motorista',
    'promover',
    'usuário existente',
    'papel',
    'senha',
    'acesso',
    'permissões',
    'perfil',
    'rota',
    'translado',
    'ida',
    'volta',
    'excluir',
    'inativo',
    'reconhecimento facial',
    'rosto',
    'biometria',
    'selfie',
    'tablet',
    'tablet do veículo',
    'primeiro acesso',
    'cadastrar rosto',
    'apagar cadastro do rosto',
    'foto do cadastro',
    'termo de ciência',
    'escalado do dia',
    'conferência',
    'código da central',
    'código no WhatsApp',
    'prova de vida',
    'LGPD',
  ],
  secoes: () => [
    {
      id: 'para-que-serve',
      titulo: 'Quem dirige',
      busca: 'motorista agente de transporte sanitário usuário conta de acesso rota translado ida volta',
      conteudo: (
        <>
          <P>
            O motorista é o agente de transporte sanitário que leva o paciente. No sistema ele é
            também um <strong>usuário</strong>: tem conta de acesso, senha e permissões, como
            qualquer pessoa da equipe — o papel dele é “motorista”.
          </P>
          <P>
            É deste cadastro que saem os nomes para escolher o motorista de uma rota, para a geração
            automática distribuir as rotas do dia e para registrar quem dirigiu na ida e na volta de
            cada viagem.
          </P>
        </>
      ),
    },
    {
      id: 'cadastrar',
      titulo: 'Cadastrar um motorista',
      busca: 'novo motorista cadastrar cpf data de nascimento continuar nome conferido cnh categoria habilitação regime de contratação clt rpa dados pessoais permissões abas',
      conteudo: (
        <>
          <Passos
            itens={[
              {
                titulo: (
                  <>
                    Em <strong>Transporte Pacientes → Motoristas</strong>, clique em{' '}
                    <BotaoRef>Novo motorista</BotaoRef>.
                  </>
                ),
              },
              {
                titulo: (
                  <>
                    Informe o CPF e a data de nascimento e clique em <BotaoRef>Continuar</BotaoRef>.
                  </>
                ),
                detalhe:
                  'O sistema confere o par e traz o nome. Nome, CPF e nascimento não se digitam nem se editam depois.',
              },
              {
                titulo: (
                  <>
                    Na aba <AbaRef>Dados pessoais</AbaRef>, complete e-mail, telefone, endereço e foto.
                  </>
                ),
              },
              {
                titulo: (
                  <>
                    Na aba <AbaRef>Motorista</AbaRef>, informe a CNH, a categoria da habilitação e
                    o regime de contratação.
                  </>
                ),
                detalhe:
                  'A CNH é obrigatória e não pode repetir a de outro motorista. Categoria (A, B, C, D, E, AB, AC, AD ou AE) e regime (CLT ou RPA) podem ficar como “Não informado” e ser completados depois.'
              },
              {
                titulo: (
                  <>
                    Na aba <AbaRef>Permissões</AbaRef>, escolha os perfis — o que ele pode ver quando
                    entrar no sistema — e clique em <BotaoRef>Cadastrar</BotaoRef>.
                  </>
                ),
              },
            ]}
          />
          <Callout tipo="regra" titulo="CPF que já está no sistema">
            Se o CPF já é de um usuário sem papel, o formulário vira{' '}
            <strong>Promover usuário existente a motorista</strong>: só se informam a CNH, a categoria e o regime, e os dados
            pessoais continuam os que ele já tinha. Se o CPF já é de um motorista, ou de alguém com
            outro papel (médico, por exemplo), o cadastro é recusado — cada pessoa tem um papel só.
          </Callout>
        </>
      ),
    },
    {
      id: 'acesso',
      titulo: 'Conta de acesso e senha',
      busca: 'senha acesso gerar senha definir senha segurança primeiro acesso trocar e-mail interno',
      conteudo: (
        <>
          <P>
            O motorista nasce <strong>sem senha</strong> que funcione. Para ele entrar, abra{' '}
            <BotaoRef variante="ghost">Editar</BotaoRef> na lista e, na aba{' '}
            <AbaRef>Dados pessoais</AbaRef>, use a seção <strong>Segurança</strong>:
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: <BotaoRef variante="outline">Gerar nova senha aleatória</BotaoRef>,
                descricao: 'O sistema cria a senha e mostra para você passar ao motorista. Ele é obrigado a trocar no primeiro acesso.',
              },
              {
                termo: <BotaoRef variante="outline">Aplicar nova senha</BotaoRef>,
                descricao:
                  'Você digita a senha (mínimo de 8 caracteres). A troca no primeiro acesso só é exigida se marcar “Exigir que o usuário troque a senha no próximo login”.',
              },
            ]}
          />
          <P>
            Sem e-mail informado, a conta fica com um endereço interno, só para existir.
          </P>
          <P>
            Essa senha é a do sistema. No <strong>tablet do veículo</strong> o motorista não usa
            senha: ele entra pelo rosto — ver “Reconhecimento facial no tablet”, logo abaixo.
          </P>
        </>
      ),
    },
    {
      id: 'lista-detalhe',
      titulo: 'Lista e detalhe',
      busca: 'lista nome foto cpf categoria cnh regime clt rpa status ativo inativo detalhe telefone translados recentes editar na lista reconhecimento facial conferências no tablet',
      conteudo: (
        <>
          <ListaDefinicoes
            itens={[
              { termo: 'Nome', descricao: 'Com a foto. Clicar abre o detalhe.' },
              { termo: 'CPF', descricao: 'O documento conferido no cadastro.' },
              { termo: 'Categoria', descricao: 'A categoria da CNH. “—” quando não foi informada.' },
              { termo: 'Regime', descricao: 'CLT ou RPA. “—” quando não foi informado.' },
              {
                termo: 'Status',
                descricao: (
                  <>
                    <SeloRef cor="sucesso">Ativo</SeloRef> quando a conta de acesso está liberada.
                    Motorista inativo não aparece para ser escolhido numa rota nem na confirmação da
                    viagem.
                  </>
                ),
              },
            ]}
          />
          <P>
            O detalhe mostra CPF, CNH, categoria da CNH, regime de contratação, telefone, a data do
            cadastro, o cartão <strong>Reconhecimento facial no tablet</strong>, as{' '}
            <strong>conferências no tablet</strong> dos últimos 7 dias e os{' '}
            <strong>translados recentes</strong> daquele motorista. Para mudar dados, use{' '}
            <BotaoRef variante="outline">Editar na lista</BotaoRef>.
          </P>
        </>
      ),
    },
    {
      id: 'reconhecimento-facial',
      titulo: 'Reconhecimento facial no tablet',
      busca:
        'reconhecimento facial rosto biometria tablet do veículo entrar pelo rosto só o escalado do dia rota do dia primeiro acesso cadastrar meu rosto cpf código whatsapp telefone do cadastro termo de ciência prova de vida piscar virar o rosto ver foto do cadastro apagar cadastro do rosto refazer primeiro acesso conferências no tablet entrada antes do translado nota mínimo aprovada recusada selfie 90 dias retenção ver no mapa código da central lgpd dado sensível',
      conteudo: (
        <>
          <P>
            O tablet do transporte é <strong>do veículo</strong>, não do motorista: fica preso no
            carro. Para usar, o motorista entra <strong>pelo rosto</strong>, que o próprio tablet
            compara com a foto do cadastro dele. E só entra quem está{' '}
            <strong>escalado no dia naquele veículo</strong> — o motorista da rota de hoje daquele
            carro (rota cancelada não conta; a volta que passa da meia-noite continua valendo).
            Antes de cada translado o tablet confere o rosto de novo.
          </P>

          <Sub>O primeiro acesso é feito pelo próprio motorista</Sub>
          <P>
            Ninguém da central cadastra o rosto. Na primeira vez, no tablet do veículo em que está
            escalado, o motorista:
          </P>
          <Passos
            itens={[
              {
                titulo: 'Toca em “Primeiro acesso (cadastrar meu rosto)” e digita o CPF.',
                detalhe:
                  'Se o CPF não é de um motorista escalado hoje naquele veículo, o tablet não manda nada.',
              },
              {
                titulo: 'Digita o código de 6 números que chega no WhatsApp do telefone do cadastro.',
                detalhe: 'O código vale 5 minutos.',
              },
              { titulo: 'Lê o termo de ciência e toca em “Concordo”.' },
              {
                titulo: 'Olha para a câmera: pisca, vira o rosto para o lado pedido e volta de frente.',
                detalhe:
                  'É a prova de vida — mostra que é uma pessoa na frente do tablet, e não uma foto. A foto do fim vira o cadastro.',
              },
            ]}
          />
          <P>
            O rosto vale para <strong>todos os tablets</strong>: o motorista cadastra uma vez só e
            entra em qualquer veículo em que for escalado.
          </P>
          <Callout tipo="atencao" titulo="O código vai para o telefone do cadastro">
            Se o telefone do motorista estiver errado ou faltando, o código não chega. Corrija em{' '}
            <BotaoRef variante="ghost">Editar</BotaoRef> e, enquanto isso, a central libera a entrada
            pelo código da central (no detalhe do veículo — ver o artigo “Veículos”).
          </Callout>

          <Sub>O cartão na ficha do motorista</Sub>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Rosto cadastrado em … no veículo …',
                descricao:
                  'Quando e no tablet de qual veículo ele fez o primeiro acesso, e a versão do termo de ciência que aceitou.',
              },
              {
                termo: 'Ainda não cadastrou',
                descricao:
                  'Ele faz no primeiro acesso, no tablet do veículo em que estiver escalado. Não há nada a fazer no painel.',
              },
              {
                termo: <BotaoRef variante="outline">Ver foto do cadastro</BotaoRef>,
                descricao: 'Abre a foto que os tablets usam para comparar. Ela só carrega quando você clica.',
              },
              {
                termo: <BotaoRef variante="outline">Apagar cadastro do rosto</BotaoRef>,
                descricao:
                  'Apaga a foto do cadastro. Use quando a foto ficou ruim e o tablet não reconhece mais o motorista, ou quando ele pedir.',
              },
            ]}
          />
          <Callout tipo="regra" titulo="Apagar o rosto obriga a refazer o primeiro acesso">
            Depois de apagado, o motorista não entra pelo rosto em nenhum tablet até refazer o
            primeiro acesso (CPF, código no WhatsApp, termo e foto nova). Enquanto isso, só entra com
            o código da central. As conferências já registradas continuam guardadas. Excluir o
            motorista também apaga o rosto dele.
          </Callout>

          <Sub>Conferências no tablet</Sub>
          <P>
            Abaixo do cartão, a tabela mostra os últimos 7 dias de conferências do motorista, as
            aprovadas e as recusadas:
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Quando',
                descricao:
                  'A hora no tablet. Se o tablet estava sem internet e mandou depois, aparece também a hora em que chegou.',
              },
              {
                termo: 'Momento',
                descricao: 'Entrada (login no tablet), Antes do translado ou Primeiro acesso.',
              },
              {
                termo: 'Como',
                descricao: 'Rosto, ou Código da central — com o nome de quem gerou o código.',
              },
              {
                termo: 'Resultado',
                descricao: (
                  <>
                    <SeloRef cor="sucesso">Aprovada</SeloRef> ou <SeloRef cor="erro">Recusada</SeloRef>,
                    com a nota e o mínimo. A nota vai de 0 a 1 e diz o quanto o rosto se pareceu com o
                    cadastro; o tablet aprova a partir do mínimo.
                  </>
                ),
              },
              { termo: 'Onde', descricao: '“Ver no mapa” abre o ponto onde o carro estava; sem GPS, “Sem posição”.' },
              {
                termo: 'Selfie',
                descricao:
                  '“Ver selfie” abre a foto que o tablet tirou na conferência — é com ela que se confere, depois, quem estava no carro. A entrada pelo código da central não tem selfie.',
              },
            ]}
          />
          <Callout tipo="lgpd" titulo="Rosto é dado pessoal sensível">
            A foto do cadastro fica no servidor porque serve a todos os tablets. As selfies das
            conferências ficam guardadas por <strong>90 dias</strong> e depois são apagadas sozinhas —
            o registro da conferência continua, e a tela avisa “Selfie apagada (retenção de 90
            dias)”. Abra as fotos só quando precisar conferir alguma coisa.
          </Callout>
        </>
      ),
    },
    {
      id: 'nas-rotas',
      titulo: 'Onde o motorista aparece',
      busca: 'translado rota montar motorista da rota geração automática distribui confirmar realização motorista da ida volta',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: 'Translado montado à mão',
              descricao: 'Ao criar a rota do dia, escolhe-se o veículo e o motorista — só os ativos aparecem.',
            },
            {
              termo: 'Geração automática',
              descricao: 'Distribui os motoristas cadastrados entre as rotas do dia; quem monta confere antes de confirmar.',
            },
            {
              termo: 'Confirmar realização da viagem',
              descricao: 'No atendimento, registra-se quem dirigiu na ida e quem dirigiu na volta.',
            },
          ]}
        />
      ),
    },
    {
      id: 'excluir',
      titulo: 'Excluir',
      busca: 'excluir motorista desligado histórico rotas conta de acesso',
      conteudo: (
        <>
          <P>
            <BotaoRef variante="danger">Excluir</BotaoRef> tira o motorista das listas e encerra a
            conta de acesso dele junto. As rotas e viagens que ele fez continuam registradas.
          </P>
          <Callout tipo="atencao" titulo="Motorista excluído não se edita">
            Depois de excluído, o cadastro fica como estava. Se a pessoa voltar, cadastre de novo.
          </Callout>
        </>
      ),
    },
    {
      id: 'quem-pode',
      titulo: 'Quem pode o quê',
      busca: 'permissão perfil módulo motoristas consulta inclusão edição exclusão',
      conteudo: (
        <>
          <P>
            A permissão é <strong>Motoristas</strong>, na tela de Perfis:
          </P>
          <ListaDefinicoes
            itens={[
              {
                termo: 'Consulta',
                descricao: 'Ver a lista e o detalhe, inclusive a situação e a foto do cadastro do rosto.',
              },
              { termo: 'Inclusão', descricao: 'Cadastrar motorista e promover usuário existente.' },
              {
                termo: 'Edição',
                descricao: 'Editar dados, CNH, categoria, regime, senha e permissões; apagar o cadastro do rosto.',
              },
              { termo: 'Exclusão', descricao: 'Excluir.' },
            ]}
          />
          <P>
            As <strong>conferências no tablet</strong> são do módulo <strong>Rastreamento</strong>:
            só aparecem na ficha para quem tem Rastreamento (Consulta).
          </P>
        </>
      ),
    },
    {
      id: 'duvidas',
      titulo: 'Dúvidas frequentes',
      busca: 'cpf já cadastrado outro papel cnh repetida não aparece na rota nome errado tablet não deixa entrar não reconheceu rosto código whatsapp não chega',
      conteudo: (
        <ListaDefinicoes
          itens={[
            {
              termo: '“CPF já cadastrado como …”',
              descricao: 'A pessoa já tem outro papel no sistema (médico, por exemplo). Uma pessoa tem um papel só.',
            },
            {
              termo: '“Já existe motorista com esta CNH.”',
              descricao: 'Outro motorista ativo usa essa CNH. Confira o número.',
            },
            {
              termo: 'O motorista não aparece para escolher na rota.',
              descricao: 'A conta dele está inativa, ou ele foi excluído.',
            },
            {
              termo: 'O nome veio diferente do que a equipe chama.',
              descricao: 'O nome vem conferido pelo CPF e não se edita — é o nome do documento.',
            },
            {
              termo: 'O tablet não deixa o motorista entrar.',
              descricao:
                'Confira se ele está escalado hoje naquele veículo (a rota do dia). Sem escala, ou se o rosto não for reconhecido — depois de 3 tentativas o próprio tablet manda pedir um código à central —, gere o código no detalhe do veículo, em Tablet vinculado → Liberar entrada de motorista.',
            },
            {
              termo: 'O código do primeiro acesso não chega no WhatsApp.',
              descricao:
                'Ele vai para o telefone do cadastro do motorista. Confira e corrija o telefone; enquanto isso, libere a entrada pelo código da central.',
            },
            {
              termo: 'O tablet parou de reconhecer o motorista.',
              descricao:
                'A foto do cadastro pode ter ficado ruim (tirada com pouca luz, por exemplo). Apague o cadastro do rosto na ficha e peça para ele refazer o primeiro acesso.',
            },
          ]}
        />
      ),
    },
  ],
};

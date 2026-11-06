<?xml version="1.0"?>
<!-- GERADO por tools/gerar_barra.py. Barra "YUNO" + menu Janela > YUNO -->
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:frmwrk="Corel Framework Data">
  <xsl:output method="xml" encoding="UTF-8" indent="yes"/>
  <frmwrk:uiconfig>
    <frmwrk:applicationInfo userConfiguration="true" />
  </frmwrk:uiconfig>

  <xsl:template match="node()|@*">
    <xsl:copy><xsl:apply-templates select="node()|@*"/></xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/items">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
      <itemData guid="4cb58535-9fa4-4688-9236-4b7887e33059" noBmpOnMenu="true" type="flyout" dynamicCategory="2cc24a3e-fe24-4708-9a74-9c75406eebcd"
                userCaption="YUNO" enable="true" flyoutBarRef="09ba2112-3fa1-4178-96e7-d230c33692f3" />
      <itemData guid="6c07c4c6-543b-4724-9e1a-91202afb8596" noBmpOnMenu="true" type="checkButton" check="*Docker('74ca6974-78e4-49ab-af1f-4804be8afc9f')"
                dynamicCategory="2cc24a3e-fe24-4708-9a74-9c75406eebcd" userCaption="YUNO (painel completo)" enable="true" />
      <itemData guid="fbe1a850-6bbc-47e4-a3fe-2bdffbc6f723" type="wpfhost" hostedType="Addons\Yuno\Yuno.dll,Yuno.UI.MainDocker" width="340" enable="true" />
      <itemData guid="ad785f27-9ec3-56e1-a6bd-c6143743e8ca" type="checkButton" check="*Docker('9e6dd6e7-adce-5e08-8b91-c4f96dcead4e')"
                userCaption="YUNO" icon="guid://ad785f27-9ec3-56e1-a6bd-c6143743e8ca" enable="true" />
      <itemData guid="eb980a26-1643-532d-87ae-348d2122da13" type="wpfhost" hostedType="Addons\Yuno\Yuno.dll,Yuno.UI.MainDocker" width="330" enable="true" />
      <itemData guid="291c9529-5e82-551e-be03-665a3d975188" type="checkButton" check="*Docker('d42ad130-bfe3-5425-a89c-8eb049c96e6c')"
                userCaption="Definir tamanhos" icon="guid://291c9529-5e82-551e-be03-665a3d975188" enable="true" />
      <itemData guid="3ec7d18d-c82f-5757-8ab0-f062261c9ecb" type="wpfhost" hostedType="Addons\Yuno\Yuno.dll,Yuno.UI.PainelDefinir" width="330" enable="true" />
      <itemData guid="22eb749a-cfbe-5c5a-86b5-544d21153074" type="checkButton" check="*Docker('02edb4fb-53e0-5dc4-aef8-366e55479653')"
                userCaption="Aplicar matrizes" icon="guid://22eb749a-cfbe-5c5a-86b5-544d21153074" enable="true" />
      <itemData guid="dc6902fe-56d4-5c99-bd7c-4a87aac1df96" type="wpfhost" hostedType="Addons\Yuno\Yuno.dll,Yuno.UI.PainelAplicar" width="330" enable="true" />
      <itemData guid="b2b8caaa-61b5-5fdc-b34c-bb0113cb6cfb" type="checkButton" check="*Docker('ae35b949-ced2-5ac3-ac17-2e2801a0f9e1')"
                userCaption="Montar pedido" icon="guid://b2b8caaa-61b5-5fdc-b34c-bb0113cb6cfb" enable="true" />
      <itemData guid="6a053b18-ed9a-5288-b936-a8da2f4f7e3a" type="wpfhost" hostedType="Addons\Yuno\Yuno.dll,Yuno.UI.PainelPedido" width="330" enable="true" />
      <itemData guid="cf1e9ff1-df68-529e-a715-feda7cc9761e" type="checkButton" check="*Docker('700ff718-0858-5b4c-b5ba-9922ff67be8b')"
                userCaption="Ferramentas" icon="guid://cf1e9ff1-df68-529e-a715-feda7cc9761e" enable="true" />
      <itemData guid="62448b85-63e8-54ff-a186-e338cee93b46" type="wpfhost" hostedType="Addons\Yuno\Yuno.dll,Yuno.UI.PainelFerramentas" width="330" enable="true" />
      <itemData guid="4300c56a-4818-5b2c-920c-54c25d35485e" type="checkButton" check="*Docker('d388a882-7133-5d1c-8ebf-29dfdc2a1229')"
                userCaption="Encaixe sobre outro" icon="guid://4300c56a-4818-5b2c-920c-54c25d35485e" enable="true" />
      <itemData guid="d8186a85-515c-5b74-8cdc-316be6c86941" type="wpfhost" hostedType="Addons\Yuno\Yuno.dll,Yuno.UI.PainelEncaixe" width="330" enable="true" />
      <itemData guid="f806f856-9c20-5867-99f4-bb53ff190a28" type="checkButton" check="*Docker('dfa57b98-d259-5419-aea5-18bbafb5cc91')"
                userCaption="Exportar" icon="guid://f806f856-9c20-5867-99f4-bb53ff190a28" enable="true" />
      <itemData guid="ef0aa7b8-8039-542f-9cfa-ade2a669f2f0" type="wpfhost" hostedType="Addons\Yuno\Yuno.dll,Yuno.UI.PainelExportar" width="330" enable="true" />
      <itemData guid="17d935f6-a152-5006-918d-cbc83d36961c" type="checkButton" check="*Docker('f8340c7e-e692-55aa-9477-1d93603b9a90')"
                userCaption="Configurações" icon="guid://17d935f6-a152-5006-918d-cbc83d36961c" enable="true" />
      <itemData guid="4854119d-129d-5b74-97a7-f13ed09ae755" type="wpfhost" hostedType="Addons\Yuno\Yuno.dll,Yuno.UI.PainelConfig" width="330" enable="true" />
    </xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/commandBars">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
      <commandBarData guid="09ba2112-3fa1-4178-96e7-d230c33692f3" type="menu" nonLocalizableName="YUNO" flyout="true"><menu /></commandBarData>
      <commandBarData guid="9992b30a-3266-5e32-88f5-5ca096b24bbd" nonLocalizableName="YUNO" userCaption="YUNO" type="toolbar">
        <toolbar>
          <item guidRef="ad785f27-9ec3-56e1-a6bd-c6143743e8ca" dock="top"/>
          <item guidRef="291c9529-5e82-551e-be03-665a3d975188" dock="top"/>
          <item guidRef="22eb749a-cfbe-5c5a-86b5-544d21153074" dock="top"/>
          <item guidRef="b2b8caaa-61b5-5fdc-b34c-bb0113cb6cfb" dock="top"/>
          <item guidRef="cf1e9ff1-df68-529e-a715-feda7cc9761e" dock="top"/>
          <item guidRef="4300c56a-4818-5b2c-920c-54c25d35485e" dock="top"/>
          <item guidRef="f806f856-9c20-5867-99f4-bb53ff190a28" dock="top"/>
          <item guidRef="17d935f6-a152-5006-918d-cbc83d36961c" dock="top"/>
        </toolbar>
      </commandBarData>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/commandBars/commandBarData[@guid='09ba2112-3fa1-4178-96e7-d230c33692f3']/menu">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
      <xsl:if test="not(./item[@guidRef='6c07c4c6-543b-4724-9e1a-91202afb8596'])"><item guidRef="6c07c4c6-543b-4724-9e1a-91202afb8596"/></xsl:if>
    </xsl:copy>
  </xsl:template>

  <xsl:template match="uiConfig/dockers">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
      <dockerData guid="74ca6974-78e4-49ab-af1f-4804be8afc9f" userCaption="YUNO" wantReturn="true" focusStyle="noThrow">
        <container><item dock="fill" margin="0,0,0,0" guidRef="fbe1a850-6bbc-47e4-a3fe-2bdffbc6f723" /></container>
      </dockerData>
      <dockerData guid="9e6dd6e7-adce-5e08-8b91-c4f96dcead4e" userCaption="YUNO" wantReturn="true" focusStyle="noThrow">
        <container><item dock="fill" margin="0,0,0,0" guidRef="eb980a26-1643-532d-87ae-348d2122da13" /></container>
      </dockerData>
      <dockerData guid="d42ad130-bfe3-5425-a89c-8eb049c96e6c" userCaption="Definir tamanhos" wantReturn="true" focusStyle="noThrow">
        <container><item dock="fill" margin="0,0,0,0" guidRef="3ec7d18d-c82f-5757-8ab0-f062261c9ecb" /></container>
      </dockerData>
      <dockerData guid="02edb4fb-53e0-5dc4-aef8-366e55479653" userCaption="Aplicar matrizes" wantReturn="true" focusStyle="noThrow">
        <container><item dock="fill" margin="0,0,0,0" guidRef="dc6902fe-56d4-5c99-bd7c-4a87aac1df96" /></container>
      </dockerData>
      <dockerData guid="ae35b949-ced2-5ac3-ac17-2e2801a0f9e1" userCaption="Montar pedido" wantReturn="true" focusStyle="noThrow">
        <container><item dock="fill" margin="0,0,0,0" guidRef="6a053b18-ed9a-5288-b936-a8da2f4f7e3a" /></container>
      </dockerData>
      <dockerData guid="700ff718-0858-5b4c-b5ba-9922ff67be8b" userCaption="Ferramentas" wantReturn="true" focusStyle="noThrow">
        <container><item dock="fill" margin="0,0,0,0" guidRef="62448b85-63e8-54ff-a186-e338cee93b46" /></container>
      </dockerData>
      <dockerData guid="d388a882-7133-5d1c-8ebf-29dfdc2a1229" userCaption="Encaixe sobre outro" wantReturn="true" focusStyle="noThrow">
        <container><item dock="fill" margin="0,0,0,0" guidRef="d8186a85-515c-5b74-8cdc-316be6c86941" /></container>
      </dockerData>
      <dockerData guid="dfa57b98-d259-5419-aea5-18bbafb5cc91" userCaption="Exportar" wantReturn="true" focusStyle="noThrow">
        <container><item dock="fill" margin="0,0,0,0" guidRef="ef0aa7b8-8039-542f-9cfa-ade2a669f2f0" /></container>
      </dockerData>
      <dockerData guid="f8340c7e-e692-55aa-9477-1d93603b9a90" userCaption="Configurações" wantReturn="true" focusStyle="noThrow">
        <container><item dock="fill" margin="0,0,0,0" guidRef="4854119d-129d-5b74-97a7-f13ed09ae755" /></container>
      </dockerData>
    </xsl:copy>
  </xsl:template>

  <!-- coloca a barra no topo, ao lado da barra de propriedades -->
  <xsl:template match="uiConfig/containers/container[@guid='bee85f91-3ad9-dc8d-48b5-d2a87c8b2109']/container[@guid='Framework_MainFrame-layout']/dockHost[@guid='894bf987-2ec1-8f83-41d8-68f6797d0db4']/toolbar[@guidRef='c2b44f69-6dec-444e-a37e-5dbf7ff43dae']">
    <xsl:copy-of select="."/>
    <toolbar guidRef="9992b30a-3266-5e32-88f5-5ca096b24bbd" />
  </xsl:template>
</xsl:stylesheet>

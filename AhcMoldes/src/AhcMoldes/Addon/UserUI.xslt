<?xml version="1.0"?>
<!-- GERADO por tools/gerar_barra.py. Insere o menu e a barra na área de trabalho do usuário. -->
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform" xmlns:frmwrk="Corel Framework Data" exclude-result-prefixes="frmwrk">
  <xsl:output method="xml" encoding="UTF-8" indent="yes"/>
  <frmwrk:uiconfig>
    <frmwrk:compositeNode xPath="/uiConfig/commandBars/commandBarData[@guid='cdb9ea9c-223e-4865-97a6-31c007c69674']"/>
    <frmwrk:compositeNode xPath="/uiConfig/commandBars/commandBarData[@guid='09ba2112-3fa1-4178-96e7-d230c33692f3']"/>
    <frmwrk:compositeNode xPath="/uiConfig/commandBars/commandBarData[@guid='9992b30a-3266-5e32-88f5-5ca096b24bbd']"/>
    <frmwrk:compositeNode xPath="/uiConfig/frame"/>
    <frmwrk:compositeNode xPath="/uiConfig/states/state[1]"/>
  </frmwrk:uiconfig>

  <xsl:template match="node()|@*">
    <xsl:copy><xsl:apply-templates select="node()|@*"/></xsl:copy>
  </xsl:template>

  <!-- faz o Corel reaplicar este arquivo a cada inicialização -->
  <xsl:template match="Addons/new_element[contains(translate(@installFile,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'\addons\ahcmoldes\userui.xslt')]"/>

  <xsl:template match="node()|@*" mode="insert-item">
    <xsl:param name="after"></xsl:param>
    <xsl:param name="content"></xsl:param>
    <xsl:copy>
      <xsl:apply-templates select="@*"/>
      <xsl:for-each select="node()">
        <xsl:copy><xsl:apply-templates select="node()|@*"/></xsl:copy>
        <xsl:if test="name()='item' and @guidRef=$after"><xsl:copy-of select="$content"/></xsl:if>
      </xsl:for-each>
      <xsl:if test="not(./item[@guidRef=$after])"><xsl:copy-of select="$content"/></xsl:if>
    </xsl:copy>
  </xsl:template>

  <!-- menu Janela -->
  <xsl:template match="commandBarData[@guid='cdb9ea9c-223e-4865-97a6-31c007c69674']/menu">
    <xsl:apply-templates mode="insert-item" select=".">
      <xsl:with-param name="content">
        <xsl:if test="not(./item[@guidRef='4cb58535-9fa4-4688-9236-4b7887e33059'])"><item guidRef="4cb58535-9fa4-4688-9236-4b7887e33059"/></xsl:if>
      </xsl:with-param>
      <xsl:with-param name="after" select="'47cc9a2d-0b7a-4df1-a686-ea3aa21b4631'"/>
    </xsl:apply-templates>
  </xsl:template>

  <xsl:template match="commandBarData[@guid='09ba2112-3fa1-4178-96e7-d230c33692f3']/menu">
    <xsl:copy>
      <xsl:apply-templates select="node()|@*"/>
      <xsl:if test="not(./item[@guidRef='6c07c4c6-543b-4724-9e1a-91202afb8596'])"><item guidRef="6c07c4c6-543b-4724-9e1a-91202afb8596"/></xsl:if>
    </xsl:copy>
  </xsl:template>

  <!-- barra no topo (layout atual e estado salvo) -->
  <xsl:template match="uiConfig/containers/container[@guid='bee85f91-3ad9-dc8d-48b5-d2a87c8b2109']/container[@guid='Framework_MainFrame-layout']/dockHost[@guid='894bf987-2ec1-8f83-41d8-68f6797d0db4']/toolbar[@guidRef='c2b44f69-6dec-444e-a37e-5dbf7ff43dae']">
    <xsl:copy><xsl:apply-templates select="node()|@*"/></xsl:copy>
    <xsl:if test="not(../toolbar[@guidRef='9992b30a-3266-5e32-88f5-5ca096b24bbd'])"><toolbar guidRef="9992b30a-3266-5e32-88f5-5ca096b24bbd"/></xsl:if>
  </xsl:template>
  <xsl:template match="uiConfig/states/state[1]/container[@guidRef='bee85f91-3ad9-dc8d-48b5-d2a87c8b2109']/layout/dockHost[@guid='894bf987-2ec1-8f83-41d8-68f6797d0db4']/toolbar[@guidRef='c2b44f69-6dec-444e-a37e-5dbf7ff43dae']">
    <xsl:copy><xsl:apply-templates select="node()|@*"/></xsl:copy>
    <xsl:if test="not(../toolbar[@guidRef='9992b30a-3266-5e32-88f5-5ca096b24bbd'])"><toolbar guidRef="9992b30a-3266-5e32-88f5-5ca096b24bbd"/></xsl:if>
  </xsl:template>
</xsl:stylesheet>

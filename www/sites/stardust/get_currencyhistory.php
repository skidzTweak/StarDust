THIS PAGE IS LOADING<?if (!$_SESSION[USERID]) header("Location: Index.php?page=Home");?><?// $sql = "select `TransactionID`, `Description`, `FromPrincipalID`, `FromName`, `FromObjectID`, `FromObjectName`, `ToPrincipalID`, `ToName`, `ToObjectID`, `ToObjectName`, `Amount`, `Complete`, `CompleteReason`, `RegionName`, `RegionID`, `RegionPos`, `TransType`, `Created`, `Updated` FROM `usercurrency_history` LIMIT 0, 30 ";if($_GET[order]=="RegionName"){	$ORDERBY=" ORDER by RegionName ASC";}else{	$ORDERBY=" ORDER by Created DESC";}$GoPage= "index.php?page=getcurrencyhistory";$AnzeigeStart = 0;// LINK SELECTOR$LinkAusgabe="page=index.php?page=getcurrencyhistory&";if($_GET[AStart]){$AStart=$_GET[AStart];};if(!$AStart) $AStart = $AnzeigeStart;$ALimit = 10;$Limit = "LIMIT $AStart, $ALimit";$NityDaysAgo = microtime(true) - 7776000;$QueryPart = " FROM stardust_currency_history WHERE (ToPrincipalID = '$_SESSION[USERID]' OR FromPrincipalID = '$_SESSION[USERID]') AND Created >= $NityDaysAgo AND Complete = 1";$DbLink->query("SELECT COUNT(*) $QueryPart" );list($count) = $DbLink->next_record();$sitemax=ceil($count / 10);$sitestart=ceil($AStart / 10)+1;if($sitemax == 0){$sitemax=1;}function getmicrotime($theTime, $e = 7) {     list($u, $s) = explode(' ',theTime);     return bcadd($u, $s, $e); } ?><div id="content">  <h2><?= SYSNAME ?>: <? echo $webui_region_list ?></h2>    <div id="regionlist">	<div id="info">		<p><? echo $webui_region_list_page_info ?></p>	</div>	<table>		<tr>			<td>				<font><b><?=$count?> <? echo $webui_regions_found; ?></b></font>			</td>			<td>			<div id="region_navigation">				<table>					<tr>						<td>							<a href="<?=$GoPage?>&<?=$Link1?>AStart=0&amp;ALimit=<?=$ALimit?>" target="_self">								<img SRC=images/icons/icon_back_more_<? if(0 > ($AStart - $ALimit)) echo off; else echo on ?>.gif WIDTH=15 HEIGHT=15 border="0" />							</a>						</td>						<td>							<a href="<?=$GoPage?>&<?=$Link1?>AStart=<? if(0 > ($AStart - $ALimit)) echo 0; else echo $AStart - $ALimit; ?>&amp;ALimit=<?=$ALimit?>" target="_self">								<img SRC=images/icons/icon_back_one_<? if(0 > ($AStart - $ALimit)) echo off; else echo on ?>.gif WIDTH=15 HEIGHT=15 border="0" />							</a>						</td>						<td>						  	<? echo $webui_navigation_page; ?> <?=$sitestart ?> <? echo $webui_navigation_of; ?> <?=$sitemax ?>						</td>						<td>							<a href="<?=$GoPage?>&<?=$Link1?>AStart=<? if($count <= ($AStart + $ALimit)) echo 0; else echo $AStart + $ALimit; ?>&amp;ALimit=<?=$ALimit?>" target="_self">								<img SRC=images/icons/icon_forward_one_<? if($count <= ($AStart + $ALimit)) echo off; else echo on ?>.gif WIDTH=15 HEIGHT=15 border="0" />							</a>						</td>						<td>							<a href="<?=$GoPage?>&<?=$Link1?>AStart=<? if(0 > ($count <= ($AStart + $ALimit))) echo 0; else echo ($sitemax - 1) * $ALimit; ?>&amp;ALimit=<?=$ALimit?>" target="_self">								<img SRC=images/icons/icon_forward_more_<? if($count <= ($AStart + $ALimit)) echo "off"; else echo "on" ?>.gif WIDTH=15 HEIGHT=15 border="0" />							</a>						</td>						<td></td>						<td>							<a href="<?=$GoPage?>&<?=$Link1?>AStart=0&amp;ALimit=10&amp;" target="_self">								<img SRC=images/icons/<? if($ALimit != 10) echo icon_limit_10_on; else echo icon_limit_off; ?>.gif WIDTH=15 HEIGHT=15 border="0" ALT="Limit 10" />							</a>						</td>						<td>							<a href="<?=$GoPage?>&<?=$Link1?>AStart=0&amp;ALimit=25&amp;" target="_self">								<img SRC=images/icons/<? if($ALimit != 25) echo icon_limit_25_on; else echo icon_limit_off; ?>.gif WIDTH=15 HEIGHT=15 border="0" ALT="Limit 25" />							</a>						</td>						<td>							<a href="<?=$GoPage?>&<?=$Link1?>AStart=0&amp;ALimit=50&amp;" target="_self">								<img SRC=images/icons/<? if($ALimit != 50) echo icon_limit_50_on; else echo icon_limit_off; ?>.gif WIDTH=15 HEIGHT=15 border="0" ALT="Limit 50" />							</a>						</td>						<td>							<a href="<?=$GoPage?>&<?=$Link1?>AStart=0&amp;ALimit=100&amp;" target="_self">								<img SRC=images/icons/<? if($ALimit != 100) echo icon_limit_100_on; else echo icon_limit_off; ?>.gif WIDTH=15 HEIGHT=15 border="0" ALT="Limit 100" />							</a>						</td>					</tr>				</table>				</div>			</td>		</tr>	</table>	<table>		<thead>			<tr>				<td width="15%">					From				</td>				<td width="15%">					To				</td>				<td width="25%">					Region				</td>				<td width="15%">					Amount				</td>				<td width="15%">					Date				</td>				<td width="15%">					Balance				</td>			</tr>		</thead>		<tbody>			<tr>				<td colspan="6">					<table width="100%">						<tbody>						<?							$w=0;							$DbLink->query("SELECT TransactionID, Description, FromPrincipalID, FromName, FromObjectID, FromObjectName, ToPrincipalID, ToName, ToObjectID, ToObjectName, Amount, Complete, CompleteReason, RegionName, RegionID, RegionPos, TransType, Created, Updated, ToBalance, FromBalance $QueryPart $ORDERBY $Limit");							while(list($TransactionID,$Description,$FromPrincipalID,$FromName, $FromObjectID, $FromObjectName, $ToPrincipalID, $ToName, $ToObjectID, $ToObjectName, $Amount, $Complete, $CompleteReason, $RegionName, $RegionID, $RegionPos, $TransType, $Created, $Updated, $ToBalance, $FromBalance) = $DbLink->next_record()){							$w++;						?>							<tr class="even" >								<td width="15%">									<div><?=$FromName?></div>								</td>								<td width="15%">									<div><?=$ToName?></div>								</td>								<td width="25%">									<div><?=$RegionName?></div>								</td>								<td width="15%">									<div><?=$Amount?></div>								</td>								<td width="15%">									<div><?=$time_output=date("m-d-y H:i",$Created)?></div>								</td>								<td width="15%" align="right">									<div><?										if ($ToPrincipalID == $_SESSION[USERID]){?>											<span style="color:green">+<?=$ToBalance?></span>										<?}else if ($FromPrincipalID == $_SESSION[USERID]){?>											<span style="color:red">-<?=$FromBalance?></span>										<?}?></div>								</td>							</tr>							<tr class="odd">								<td colspan="6" width="100%">									<? if (($ToObjectName != "") || ($FromObjectName != "")){ ?>										<?=$ToObjectName?><?=$FromObjectName?>:									<?}?>									<?=$Description?>								</td>							</tr>						<?}?>						</tbody>					</table>				</td>			</tr>		</tbody>	</table></div></div>
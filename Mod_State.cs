using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DesignPattern_HSMediator;
using CFPGADrv;
using BYTE = System.Byte;
using UnityEngine.UI;
using System.Runtime.InteropServices;
using System;
public class Mod_State : IGameSystem
{

    public volatile System.UInt32 read_long_result1, read_long_result2, read_long_result_stored1, read_long_result_stored2;

    [DllImport("libqxt")]
    public static extern void qxt_device_init();

    [DllImport("libqxt")]
    public static extern UInt32 qxt_dio_readdword(UInt32 offset);

    [DllImport("libqxt")]
    public static extern Byte qxt_dio_readword(UInt32 offset);

    [DllImport("libqxt")]
    public static extern Byte qxt_dio_writeword(UInt32 offset, UInt32 Value);

    [DllImport("libqxt")]
    public static extern UInt32 qxt_dio_readword_fb(UInt32 offset);

    [DllImport("libqxt")]
    public static extern byte qxt_dio_setbit(UInt32 offset, long bitmask);

    public enum STATE
    {
        BaseSpin, BaseScrolling, BaseEnd, BaseRollScore, BonustransIn, BonusSpin, BonusScrolling, BonusEnd, BonusRollScore, BonusTransOut, GetBonusInBonus, AfterBonusRollScore
    };
    public enum EVENT
    {
        ENTER, UPDATE, EXIT
    };
    public STATE stateName;
    protected EVENT stage;
    protected Mod_State nextState;


    public Mod_State()
    {
        stage = EVENT.ENTER;
    }

    public virtual void Enter() { Mod_Data.state = this.stateName; Mod_Data.BlankClick = false; m_SlotMediatorController.SendMessage("m_state", "ReregisterState"); stage = EVENT.UPDATE; }
    public virtual void Update() { stage = EVENT.UPDATE; }
    public virtual void Exit() { stage = EVENT.EXIT; }
    public Mod_State Process()
    {
        if (stage == EVENT.ENTER) Enter();
        if (stage == EVENT.UPDATE) Update();
        if (stage == EVENT.EXIT)
        {
            Exit();
            return nextState;
        }
        return this;
    }
    public virtual void SpecialOrder()
    {
        //  Debug.Log("Testtest123");
    }
}


//開始滾輪前
public class BaseSpin : Mod_State
{
    CFPGADrvBridge.STATUS Status = new CFPGADrvBridge.STATUS(); //賽菲硬體初始化
    byte DataByte = 1;//賽飛訊號
    float ClearButtonHoldTime = 0;
    public bool[] ButtonClickLong = new bool[32];//賽菲按鈕
    public BaseSpin() : base()//初始化
    {

        stateName = STATE.BaseSpin;
    }

    public override void Enter()//Enter階段
    {
        if (BillAcceptorSettingData.BillOpenClose)
        {
            BillAcceptorSettingData.BillAcceptorEnable = true;
            BillAcceptorSettingData.GetOrderType = "BillEnableDisable";
        }

        Mod_Data.BonusCount = 0;
        if (Mod_Data.Win > 0)//有贏分跑線時  開起來
        {
            m_SlotMediatorController.SendMessage("m_state", "OpenBlankButton");
        }
        else
        {
            m_SlotMediatorController.SendMessage("m_state", "CloseBlankButton");
        }
        if (Mod_Data.credit - Mod_Data.Bet * Mod_Data.odds * Mod_Data.Denom < 0)//彩分低於壓住分數時,停止自動遊戲
        {
            Mod_Data.autoPlay = false;
        }
        Mod_Data.inBaseSpin = true; //偵測是否在BaseSpin
        Mod_Data.StartNotNormal = false;//是否正常遊戲判斷,預設Mod_Data.StartNotNormal = true
        BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.status, 0);//設定狀態=BaseSpin
        base.Enter();

    }
    float autoTimer = 0;
    bool hardSpaceButtonDown = false;


    public override void Update()//Update階段
    {
        for (int i = 0; i < 32; i++)
        {
            Status = (CFPGADrvBridge.STATUS)CFPGADrvBridge.FD_GetGPIByIndex(0, (BYTE)i, ref DataByte);
            if (DataByte == 0 && !ButtonClickLong[i])
            {
                ButtonClickLong[i] = true;
                SephirothButton(i);
            }
            else if (DataByte != 0)
            {
                ButtonClickLong[i] = false;
            }
        }

#if UNITY_EDITOR
       if (Input.GetKeyDown(KeyCode.Space))
        {
            hardSpaceButtonDown = true;
        }
#endif
        #region 勝圖硬體<-------------------
        read_long_result1 = qxt_dio_readdword(0);  //讀取輸入訊號
        read_long_result2 = qxt_dio_readdword(4);
        if (qxt_dio_readword(1) != 255) { print(qxt_dio_readword(1)); }
        if (read_long_result1 != read_long_result_stored1)
        {
            switch (read_long_result1)
            {   //按鈕順序由左至右
                case 0xFFFFFFFB:
                    print("出票");//停一
                    Debug.Log("Mod_Data.IOLock: " + Mod_Data.IOLock + " Mod_Data.MachineError: " + Mod_Data.MachineError + " Mod_Data.PrinterTicket: " + Mod_Data.PrinterTicket + " Mod_Data.memberLcok: " + Mod_Data.memberLcok + "  Mod_Data.credit: " + Mod_Data.credit);
                    if (!Mod_Data.IOLock && !Mod_Data.autoPlay && !Mod_Data.MachineError && !Mod_Data.PrinterTicket && !Mod_Data.memberLcok && Mod_Data.credit > 0 && !hardSpaceButtonDown)
                    {
                        Mod_Data.PrinterTicket = true;
                        Mod_Data.BlankClick = true;
                        GameObject.Find("Printer").GetComponent<Mod_Gen2_Status>().PrintTikcet();
                    }
                    break;
                case 0xFFFFFFFD:
                    print("服務鈴");//停二
                    break;
                case 0xFFDFFFFF:
                    print("押注1");//停三 
                    break;
                case 0xFFBFFFFF:
                    print("押注2");//停四 -
                    break;
                case 0xFF7FFFFF:
                    print("押注3");//停五 +
                    break;
                case 0xFEFFFFFF:
                    print("押注4"); // most
                    if (!Mod_Data.IOLock && !Mod_Data.autoPlay && !Mod_Data.winErrorLock && !Mod_Data.creditErrorLock && !Mod_Data.monthLock && !Mod_Data.billLock && !Mod_TimeController.GamePasue && !Mod_Data.MachineError && !Mod_Data.memberLcok)
                    {
                        Mod_Data.Bet = Mod_Data.BetOri;
                        if (Mod_Data.odds > 1)
                        {
                            Mod_Data.odds--;
                            if (!Mod_Data.afterBonus) m_SlotMediatorController.SendMessage("m_state", "StopAllGameFrame");
                            else Mod_Data.Win = 0;
                            m_SlotMediatorController.SendMessage("m_state", "UpdateUIscore");
                        }
                    }
                    break;
                case 0xFDFFFFFF:
                    print("押注5");//auto
                    if (!Mod_Data.IOLock && !Mod_Data.autoPlay && !Mod_Data.winErrorLock && !Mod_Data.creditErrorLock && !Mod_Data.monthLock && !Mod_Data.billLock && !Mod_TimeController.GamePasue && !Mod_Data.MachineError && !Mod_Data.memberLcok)
                    {
                        Mod_Data.Bet = Mod_Data.BetOri;
                        if (Mod_Data.odds < Mod_Data.maxOdds)
                        {
                            Mod_Data.odds++;
                            if (!Mod_Data.afterBonus) m_SlotMediatorController.SendMessage("m_state", "StopAllGameFrame");
                            else Mod_Data.Win = 0;
                            m_SlotMediatorController.SendMessage("m_state", "UpdateUIscore");
                        }

                    }
                    break;
                case 0xFFFFFFFE:
                    print("開始");
                    if (!Mod_Data.IOLock && !Mod_Data.autoPlay && !Mod_Data.winErrorLock && !Mod_Data.creditErrorLock && !Mod_Data.monthLock && !Mod_Data.billLock && !Mod_TimeController.GamePasue && !Mod_Data.MachineError && !Mod_Data.memberLcok)
                    {
                        hardSpaceButtonDown = true;
                    }

                    break;
                case 0x7FFFFFFF:     //轉鑰匙時的訊號 開啟後台用
                    if (!Mod_Data.autoPlay && !Mod_Data.winErrorLock && !Mod_Data.creditErrorLock && !Mod_Data.IOLock)
                    {
                        Mod_Data.IOLock = true;
                        if (!Mod_Data.afterBonus) m_SlotMediatorController.SendMessage("m_state", "StopAllGameFrame");
                        m_SlotMediatorController.SendMessage("m_state", "OpenLogin");
                    }

                    break;
            }
            read_long_result_stored1 = qxt_dio_readdword(0);
        }

        #endregion

        if (Mod_Data.odds > Mod_Data.maxOdds) Mod_Data.odds = Mod_Data.maxOdds;

        //彩分大於最大彩分,鎖定並跳警告
        if (Mod_Data.credit > Mod_Data.maxCredit)
        {
            if (!Mod_Data.creditErrorLock && !Mod_Data.monthLock)
            {
                Debug.Log(Mod_Data.credit + "," + Mod_Data.maxCredit);
                m_SlotMediatorController.SendMessage("m_state", "ErrorCreditOpen");
                Mod_Data.creditErrorLock = true;
                Mod_Data.autoPlay = false;
            }

        }
        else
        {
            if (!Mod_Data.winErrorLock && !Mod_Data.monthLock)
            {
                m_SlotMediatorController.SendMessage("m_state", "ErrorCreditClose");
                Mod_Data.creditErrorLock = false;
            }


        }
        //贏分大於最大贏分時,鎖定並警告
        if (Mod_Data.Win > Mod_Data.maxWin)
        {
            if (!Mod_Data.winErrorLock && !Mod_Data.monthLock)
            {
                m_SlotMediatorController.SendMessage("m_state", "ErrorWinOpen");
                Mod_Data.winErrorLock = true;
                Mod_Data.autoPlay = false;
            }

        }
        else
        {
            if (!Mod_Data.creditErrorLock && !Mod_Data.monthLock)
            {
                m_SlotMediatorController.SendMessage("m_state", "ErrorWinClose");
                Mod_Data.winErrorLock = false;
            }
        }

        if (Mod_Data.monthLock && !Mod_Data.creditErrorLock && !Mod_Data.winErrorLock)
        {
            m_SlotMediatorController.SendMessage("m_state", "ErrorMonthLockOpen");
        }
        if (!Mod_Data.monthLock && !Mod_Data.creditErrorLock && !Mod_Data.winErrorLock)
        {
            m_SlotMediatorController.SendMessage("m_state", "ErrorMonthLockClose");
        }
        //if (Mod_Data.memberLcok)
        //{
        //    m_SlotMediatorController.SendMessage("m_state", "memberLock",1);
        //    Mod_Data.autoPlay = false;
        //}
        //else
        //{
        //    m_SlotMediatorController.SendMessage("m_state", "memberLock", 0);
           
        //}
        if (Mod_Data.severHistoryLock) return;
        if (!Mod_Data.IOLock && !Mod_Data.winErrorLock && !Mod_Data.creditErrorLock && !Mod_Data.monthLock && !Mod_Data.billLock && !Mod_Data.MachineError && !Mod_TimeController.GamePasue && !Mod_Data.memberLcok)
        {

            if (Mod_Data.credit < 0.01f) Mod_Data.credit = 0;
            //如果點擊螢幕,停止跑線&TakeWin
            if (Mod_Data.BlankClick)
            {
                Mod_Data.BlankClick = false;
                m_SlotMediatorController.SendMessage("m_state", "CloseBlankButton");
                if (!Mod_Data.afterBonus) m_SlotMediatorController.SendMessage("m_state", "StopAllGameFrame");
                else Mod_Data.Win = 0;
                m_SlotMediatorController.SendMessage("m_state", "UpdateUIscore");
                autoTimer++;
            }
            //自動遊戲計時
            if (Mod_Data.autoPlay)
            {
                autoTimer += Time.deltaTime;
                if (autoTimer > 4)
                    autoTimer = 0;

            }

            if (((hardSpaceButtonDown) || (Mod_Data.autoPlay && autoTimer > 2) || (Mod_Data.autoPlay && Mod_Data.Win <= 0)) && Mod_Data.odds >= 1 && !Mod_Data.MachineError && !Mod_TimeController.GamePasue && !Mod_Data.PrinterTicket && !Mod_Data.memberLcok && ((BillAcceptorSettingData.BillOpenClose && BillAcceptorSettingData.GameCanPlay) || !BillAcceptorSettingData.BillOpenClose))
            {

                Debug.Log("Start Reel!!!!!!!!!!!");
                Mod_Data.linegame_LineCount = Mod_Data.linegame_LineCountOri;//初始化線數
                Mod_Data.Bet = Mod_Data.BetOri;//初始化押注
                //彩分高於目前設定壓注率及倍率(正常押注)
                if (Mod_Data.credit - Mod_Data.Bet * Mod_Data.odds * Mod_Data.Denom >= 0)
                {

                    Mod_Data.credit -= Mod_Data.Bet * Mod_Data.odds * Mod_Data.Denom;
                    Mod_Data.betLowCreditShowOnce = false;



                    //遊戲計算
                    m_SlotMediatorController.SendMessage("m_state", "DisableAllLine");//消除餘分線
                    m_SlotMediatorController.SendMessage("m_state", "UpdateUIscore");//更新分數
                    m_SlotMediatorController.SendMessage("m_state", "SetReel");//更新分數
                    m_SlotMediatorController.SendMessage("m_state", "CheckBonus");//偵測Bonus
                    m_SlotMediatorController.SendMessage("m_state", "GameMathCount");
                    Mod_Data.credit += Mod_Data.Win * Mod_Data.Denom;//遊戲要儲存加贏分之後的彩分
                    m_SlotMediatorController.SendMessage("m_state", "ServerWork", (int)Mod_Client_Data.messagetype.gamehistory);
                    m_SlotMediatorController.SendMessage("m_state", "SaveData");//儲存資料
                    if (!Mod_Data.getBonus) m_SlotMediatorController.SendMessage("m_state", "ComparisonMaxWin");//儲存最大贏分
                    Mod_Data.credit -= Mod_Data.Win * Mod_Data.Denom;//儲存完成後先扣掉贏分
                    m_SlotMediatorController.SendMessage("m_state", "GetLocalGameRound");
                    m_SlotMediatorController.SendMessage("m_state", "ServerWork", (int)Mod_Client_Data.messagetype.gamehistory);
                    m_SlotMediatorController.SendMessage("m_state", "SaveLocalGameRound");



                    /*紀錄帳務資訊*/
                    BackEnd_Data.SetDouble(BackEnd_Data.SramAccountData.totalBet, BackEnd_Data.GetDouble(BackEnd_Data.SramAccountData.totalBet) + Mod_Data.Bet * Mod_Data.odds * Mod_Data.Denom);
                    BackEnd_Data.SetDouble(BackEnd_Data.SramAccountData.totalBet_Class, BackEnd_Data.GetDouble(BackEnd_Data.SramAccountData.totalBet_Class) + Mod_Data.Bet * Mod_Data.odds * Mod_Data.Denom);
                    BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.gameCount, BackEnd_Data.GetInt(BackEnd_Data.SramAccountData.gameCount) + 1);
                    BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.gameCount_Class, BackEnd_Data.GetInt(BackEnd_Data.SramAccountData.gameCount_Class) + 1);
                    if (Mod_Data.Win > 0 && !Mod_Data.getBonus)
                    {
                        BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.winCount, BackEnd_Data.GetInt(BackEnd_Data.SramAccountData.winCount) + 1);
                        BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.winCount_Class, BackEnd_Data.GetInt(BackEnd_Data.SramAccountData.winCount_Class) + 1);
                        BackEnd_Data.SetDouble(BackEnd_Data.SramAccountData.totalWin, BackEnd_Data.GetDouble(BackEnd_Data.SramAccountData.totalWin) + Mod_Data.Win * Mod_Data.Denom);
                        BackEnd_Data.SetDouble(BackEnd_Data.SramAccountData.totalWin_Class, BackEnd_Data.GetDouble(BackEnd_Data.SramAccountData.totalWin_Class) + Mod_Data.Win * Mod_Data.Denom);
                    }
                    //轉換狀態
                    nextState = new BaseScrolling();
                    nextState.setMediator(m_SlotMediatorController);
                    stage = EVENT.EXIT;
                }

                //餘分計算(餘分變化規則 降低押注率odds,押注降至最高且低於彩分之押注率,若押注率=1時仍無法低於彩分,則降低倍率Denom,降低倍率時,押注率升至最高且可負擔制押注率)
                //Ex1.1:1 彩分23分 最低押注25, odd=1,倍率(Denom降至0.5)彩分46分 最低押注25, odd=1,開始遊戲
                //Ex2.1:1 彩分20分  最低押注25, odd=1,倍率(Denom降至0.1)彩分200分, odds提高至可負擔押住率 odds=8 ,押注=200, 開始遊戲

                else if (Mod_Data.credit > 0)
                {
                    double tmpodds = Mod_Data.odds, tmpDenom = Mod_Data.Denom;
                    bool denomLeast = true, oddLeast = true;

                    //降至可負擔之最小倍率,還無法負擔時,降賭注賠率
                    for (int i = (int)Mod_Data.odds; i >= 1; i--)
                    {
                        if (Mod_Data.credit - Mod_Data.Bet * i * Mod_Data.Denom >= 0)
                        {
                            Mod_Data.odds = i;
                            Debug.Log("Odds" + i);
                            break;
                        }
                    }

                    //Mod_Data.betLowCreditShowOnce 為顯示餘分線之判斷,若odds有更動時 如Ex1,則顯示一次餘分線後開始遊戲
                    if (!Mod_Data.betLowCreditShowOnce && tmpodds != Mod_Data.odds)//彩分低於最低賭注 顯示一次
                    {
                        Mod_Data.betLowCreditShowOnce = true;
                        Mod_Data.autoPlay = false;
                        m_SlotMediatorController.SendMessage("m_state", "StopAllGameFrame");
                        m_SlotMediatorController.SendMessage("m_state", "EnableAllLine");  //顯示餘分線
                        m_SlotMediatorController.SendMessage("m_state", "UpdateUIscore");
                    }
                    else
                    {
                        for (int i = (int)Mod_Data.odds; i >= 1; i--)
                        {
                            if (Mod_Data.credit - Mod_Data.Bet * i * Mod_Data.Denom >= 0)//降至可負擔之最小倍率,還無法負擔時,降賭注賠率
                            {
                                Mod_Data.odds = i;
                                Mod_Data.credit -= Mod_Data.Bet * i * Mod_Data.Denom;
                                Mod_Data.betLowCreditShowOnce = false;



                                //遊戲計算
                                m_SlotMediatorController.SendMessage("m_state", "DisableAllLine");//消除餘分線
                                m_SlotMediatorController.SendMessage("m_state", "UpdateUIscore");//更新分數
                                m_SlotMediatorController.SendMessage("m_state", "SetReel");//更新分數
                                m_SlotMediatorController.SendMessage("m_state", "CheckBonus");//偵測Bonus
                                m_SlotMediatorController.SendMessage("m_state", "GameMathCount");
                                Mod_Data.credit += Mod_Data.Win * Mod_Data.Denom;//遊戲要儲存加贏分之後的彩分
                                m_SlotMediatorController.SendMessage("m_state", "ServerWork", (int)Mod_Client_Data.messagetype.gamehistory);
                                m_SlotMediatorController.SendMessage("m_state", "SaveData");//儲存資料
                                if (!Mod_Data.getBonus) m_SlotMediatorController.SendMessage("m_state", "ComparisonMaxWin");//儲存最大贏分
                                Mod_Data.credit -= Mod_Data.Win * Mod_Data.Denom;//儲存完成後先扣掉贏分
                                m_SlotMediatorController.SendMessage("m_state", "GetLocalGameRound");
                                m_SlotMediatorController.SendMessage("m_state", "ServerWork", (int)Mod_Client_Data.messagetype.gamehistory);
                                m_SlotMediatorController.SendMessage("m_state", "SaveLocalGameRound");




                                /*紀錄帳務資訊*/
                                BackEnd_Data.SetDouble(BackEnd_Data.SramAccountData.totalBet, BackEnd_Data.GetDouble(BackEnd_Data.SramAccountData.totalBet) + Mod_Data.Bet * i * Mod_Data.Denom);
                                BackEnd_Data.SetDouble(BackEnd_Data.SramAccountData.totalBet_Class, BackEnd_Data.GetDouble(BackEnd_Data.SramAccountData.totalBet_Class) + Mod_Data.Bet * i * Mod_Data.Denom);
                                BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.gameCount, BackEnd_Data.GetInt(BackEnd_Data.SramAccountData.gameCount) + 1);
                                BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.gameCount_Class, BackEnd_Data.GetInt(BackEnd_Data.SramAccountData.gameCount_Class) + 1);
                                if (Mod_Data.Win > 0 && !Mod_Data.getBonus)
                                {
                                    BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.winCount, BackEnd_Data.GetInt(BackEnd_Data.SramAccountData.winCount) + 1);
                                    BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.winCount_Class, BackEnd_Data.GetInt(BackEnd_Data.SramAccountData.winCount_Class) + 1);
                                    BackEnd_Data.SetDouble(BackEnd_Data.SramAccountData.totalWin, BackEnd_Data.GetDouble(BackEnd_Data.SramAccountData.totalWin) + Mod_Data.Win * Mod_Data.Denom);
                                    BackEnd_Data.SetDouble(BackEnd_Data.SramAccountData.totalWin_Class, BackEnd_Data.GetDouble(BackEnd_Data.SramAccountData.totalWin_Class) + Mod_Data.Win * Mod_Data.Denom);
                                }

                                //轉換狀態
                                nextState = new BaseScrolling();
                                nextState.setMediator(m_SlotMediatorController);
                                oddLeast = false;
                                stage = EVENT.EXIT;
                                break;
                            }
                        }
                    }

                    //若odds無更動時,表示需調整倍率 如Ex2
                    if (oddLeast && (tmpodds == Mod_Data.odds))
                    {
                        Mod_Data.odds = 1;
                        //降至可負擔之最大倍率
                        for (int i = 0; i < Mod_Data.denomArray.Length; i++)
                        {
                            if (Mod_Data.denomOpenArray[i])
                            {
                                if (Mod_Data.credit - Mod_Data.Bet * Mod_Data.odds * Mod_Data.denomArray[i] >= 0)
                                {
                                    Mod_Data.Denom = Mod_Data.denomArray[i];
                                    break;
                                }
                            }
                        }
                        //降至可負擔之最大倍率,押注率升至最高且可負擔之押注率
                        for (int i = (int)Mod_Data.maxOdds; i >= 1; i--)
                        {
                            if (Mod_Data.credit - Mod_Data.Bet * i * Mod_Data.Denom >= 0)
                            {
                                Mod_Data.odds = i;
                                break;
                            }
                        }
                        if (!Mod_Data.betLowCreditShowOnce && tmpDenom != Mod_Data.Denom)//彩分低於最低賭注 顯示一次
                        {
                            Mod_Data.betLowCreditShowOnce = true;
                            Mod_Data.autoPlay = false;
                            m_SlotMediatorController.SendMessage("m_state", "StopAllGameFrame");
                            m_SlotMediatorController.SendMessage("m_state", "EnableAllLine");
                            m_SlotMediatorController.SendMessage("m_state", "UpdateUIscore");
                        }
                        else
                        {
                            for (int i = 0; i < Mod_Data.denomArray.Length; i++)
                            {
                                if (Mod_Data.denomOpenArray[i])
                                {
                                    if (Mod_Data.credit - Mod_Data.Bet * Mod_Data.odds * Mod_Data.denomArray[i] >= 0)//降至可負擔之最大倍率
                                    {

                                        Mod_Data.Denom = Mod_Data.denomArray[i];
                                        Mod_Data.credit -= Mod_Data.Bet * Mod_Data.odds * Mod_Data.Denom;
                                        Mod_Data.betLowCreditShowOnce = false;



                                        //遊戲計算
                                        m_SlotMediatorController.SendMessage("m_state", "DisableAllLine");//消除餘分線
                                        m_SlotMediatorController.SendMessage("m_state", "UpdateUIscore");//更新分數
                                        m_SlotMediatorController.SendMessage("m_state", "SetReel");//更新分數
                                        m_SlotMediatorController.SendMessage("m_state", "CheckBonus");//偵測Bonus
                                        m_SlotMediatorController.SendMessage("m_state", "GameMathCount");
                                        Mod_Data.credit += Mod_Data.Win * Mod_Data.Denom;//遊戲要儲存加贏分之後的彩分
                                        m_SlotMediatorController.SendMessage("m_state", "ServerWork", (int)Mod_Client_Data.messagetype.gamehistory);
                                        m_SlotMediatorController.SendMessage("m_state", "SaveData");//儲存資料
                                        if (!Mod_Data.getBonus) m_SlotMediatorController.SendMessage("m_state", "ComparisonMaxWin");//儲存最大贏分
                                        Mod_Data.credit -= Mod_Data.Win * Mod_Data.Denom;//儲存完成後先扣掉贏分
                                        m_SlotMediatorController.SendMessage("m_state", "GetLocalGameRound");
                                        m_SlotMediatorController.SendMessage("m_state", "ServerWork", (int)Mod_Client_Data.messagetype.gamehistory);
                                        m_SlotMediatorController.SendMessage("m_state", "SaveLocalGameRound");



                                        /*紀錄帳務資訊*/
                                        BackEnd_Data.SetDouble(BackEnd_Data.SramAccountData.totalBet, BackEnd_Data.GetDouble(BackEnd_Data.SramAccountData.totalBet) + Mod_Data.Bet * Mod_Data.odds * Mod_Data.Denom);
                                        BackEnd_Data.SetDouble(BackEnd_Data.SramAccountData.totalBet_Class, BackEnd_Data.GetDouble(BackEnd_Data.SramAccountData.totalBet_Class) + Mod_Data.Bet * Mod_Data.odds * Mod_Data.Denom);
                                        BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.gameCount, BackEnd_Data.GetInt(BackEnd_Data.SramAccountData.gameCount) + 1);
                                        BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.gameCount_Class, BackEnd_Data.GetInt(BackEnd_Data.SramAccountData.gameCount_Class) + 1);
                                        if (Mod_Data.Win > 0 && !Mod_Data.getBonus)
                                        {
                                            BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.winCount, BackEnd_Data.GetInt(BackEnd_Data.SramAccountData.winCount) + 1);
                                            BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.winCount_Class, BackEnd_Data.GetInt(BackEnd_Data.SramAccountData.winCount_Class) + 1);
                                            BackEnd_Data.SetDouble(BackEnd_Data.SramAccountData.totalWin, BackEnd_Data.GetDouble(BackEnd_Data.SramAccountData.totalWin) + Mod_Data.Win * Mod_Data.Denom);
                                            BackEnd_Data.SetDouble(BackEnd_Data.SramAccountData.totalWin_Class, BackEnd_Data.GetDouble(BackEnd_Data.SramAccountData.totalWin_Class) + Mod_Data.Win * Mod_Data.Denom);
                                        }
                                        //轉換狀態
                                        nextState = new BaseScrolling();
                                        nextState.setMediator(m_SlotMediatorController);
                                        denomLeast = false;
                                        stage = EVENT.EXIT;
                                        break;
                                    }
                                }
                            }
                        }
                    }

                    //若倍率及押注率都最低的情況下還無法負擔時,將押注分數降低至目前彩分
                    if (denomLeast && oddLeast && Mod_Data.currentGameRule == Mod_Data.SlotGameRule.LineGame && (tmpodds == Mod_Data.odds) && (tmpDenom == Mod_Data.Denom))
                    {
                        Mod_Data.odds = 1;
                        for (int i = Mod_Data.denomArray.Length - 1; i >= 0; i--)
                        {
                            if (Mod_Data.denomOpenArray[i])
                            {
                                Mod_Data.Denom = Mod_Data.denomArray[i];
                                Debug.Log("Denom" + Mod_Data.Denom);
                                break;
                            }
                        }
                        Mod_Data.linegame_LineCount = Mathf.CeilToInt((float)(Mod_Data.credit / Mod_Data.Denom));
                        Mod_Data.Bet = Mod_Data.linegame_LineCount;
                        //Debug.Log("BaseSpin1: " + Mod_Data.Bet);
                        if (!Mod_Data.betLowCreditShowOnce)//彩分低於最低賭注 顯示一次
                        {
                            Mod_Data.betLowCreditShowOnce = true;
                            m_SlotMediatorController.SendMessage("m_state", "StopAllGameFrame");
                            m_SlotMediatorController.SendMessage("m_state", "EnableAllLine");
                            m_SlotMediatorController.SendMessage("m_state", "UpdateUIscore");

                        }
                        else
                        {
                            Mod_Data.odds = 1;
                            Mod_Data.Bet = Mathf.CeilToInt((float)(Mod_Data.credit / Mod_Data.Denom));
                            Mod_Data.Bet = Mod_Data.linegame_LineCount;
                            Mod_Data.credit = 0;
                            Mod_Data.betLowCreditShowOnce = false;




                            //遊戲計算
                            m_SlotMediatorController.SendMessage("m_state", "DisableAllLine");//消除餘分線
                            m_SlotMediatorController.SendMessage("m_state", "UpdateUIscore");//更新分數
                            m_SlotMediatorController.SendMessage("m_state", "SetReel");//更新分數
                            m_SlotMediatorController.SendMessage("m_state", "CheckBonus");//偵測Bonus
                            m_SlotMediatorController.SendMessage("m_state", "GameMathCount");
                            Mod_Data.credit += Mod_Data.Win * Mod_Data.Denom;//遊戲要儲存加贏分之後的彩分
                            m_SlotMediatorController.SendMessage("m_state", "ServerWork", (int)Mod_Client_Data.messagetype.gamehistory);
                            m_SlotMediatorController.SendMessage("m_state", "SaveData");//儲存資料
                            if (!Mod_Data.getBonus) m_SlotMediatorController.SendMessage("m_state", "ComparisonMaxWin");//儲存最大贏分
                            Mod_Data.credit -= Mod_Data.Win * Mod_Data.Denom;//儲存完成後先扣掉贏分
                            m_SlotMediatorController.SendMessage("m_state", "GetLocalGameRound");
                            m_SlotMediatorController.SendMessage("m_state", "ServerWork", (int)Mod_Client_Data.messagetype.gamehistory);
                            m_SlotMediatorController.SendMessage("m_state", "SaveLocalGameRound");


                            /*紀錄帳務資訊*/
                            BackEnd_Data.SetDouble(BackEnd_Data.SramAccountData.totalBet, BackEnd_Data.GetDouble(BackEnd_Data.SramAccountData.totalBet) + Mod_Data.Bet * Mod_Data.Denom);
                            BackEnd_Data.SetDouble(BackEnd_Data.SramAccountData.totalBet_Class, BackEnd_Data.GetDouble(BackEnd_Data.SramAccountData.totalBet_Class) + Mod_Data.Bet * Mod_Data.Denom);
                            BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.gameCount, BackEnd_Data.GetInt(BackEnd_Data.SramAccountData.gameCount) + 1);
                            BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.gameCount_Class, BackEnd_Data.GetInt(BackEnd_Data.SramAccountData.gameCount_Class) + 1);
                            if (Mod_Data.Win > 0 && !Mod_Data.getBonus)
                            {
                                BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.winCount, BackEnd_Data.GetInt(BackEnd_Data.SramAccountData.winCount) + 1);
                                BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.winCount_Class, BackEnd_Data.GetInt(BackEnd_Data.SramAccountData.winCount_Class) + 1);
                                BackEnd_Data.SetDouble(BackEnd_Data.SramAccountData.totalWin, BackEnd_Data.GetDouble(BackEnd_Data.SramAccountData.totalWin) + Mod_Data.Win * Mod_Data.Denom);
                                BackEnd_Data.SetDouble(BackEnd_Data.SramAccountData.totalWin_Class, BackEnd_Data.GetDouble(BackEnd_Data.SramAccountData.totalWin_Class) + Mod_Data.Win * Mod_Data.Denom);
                            }



                            nextState = new BaseScrolling();
                            nextState.setMediator(m_SlotMediatorController);
                            oddLeast = false;

                            stage = EVENT.EXIT;
                        }
                        Debug.Log(Mod_Data.betLowCreditShowOnce + "2");
                    }

                }//餘分計算
                else//如果沒錢
                {
                    Mod_Data.autoPlay = false;
                }
            }



            //-----鍵盤控制

            //// 押注-
            //if (Input.GetKeyDown(KeyCode.R))
            //{  
            //    if (!Mod_Data.IOLock && !Mod_Data.autoPlay && !Mod_Data.winErrorLock && !Mod_Data.creditErrorLock)
            //    {
            //        Mod_Data.Bet = Mod_Data.BetOri;
            //        if (Mod_Data.odds > 1)
            //        {
            //            Mod_Data.odds--;
            //            if (!Mod_Data.afterBonus) m_SlotMediatorController.SendMessage("m_state", "StopAllGameFrame");
            //            else Mod_Data.Win = 0;
            //            m_SlotMediatorController.SendMessage("m_state", "UpdateUIscore");
            //        }
            //    }
            //}
            //// 押注+
            //if (Input.GetKeyDown(KeyCode.T))
            //{  
            //    if (!Mod_Data.IOLock && !Mod_Data.autoPlay && !Mod_Data.winErrorLock && !Mod_Data.creditErrorLock)
            //    {
            //        Mod_Data.Bet = Mod_Data.BetOri;
            //        if (Mod_Data.odds < Mod_Data.maxOdds)
            //        {
            //            Mod_Data.odds++;
            //            if (!Mod_Data.afterBonus) m_SlotMediatorController.SendMessage("m_state", "StopAllGameFrame");
            //            else Mod_Data.Win = 0;
            //            m_SlotMediatorController.SendMessage("m_state", "UpdateUIscore");
            //        }

            //    }
            //}
            ////自動
            //if (Input.GetKeyDown(KeyCode.A))
            //{  
            //    if (!Mod_Data.IOLock && !Mod_Data.winErrorLock && !Mod_Data.creditErrorLock) Mod_Data.autoPlay = !Mod_Data.autoPlay;
            //}
            ////最大押注
            //if (Input.GetKeyDown(KeyCode.S) && !Mod_Data.autoPlay)
            //{
            //    if (!Mod_Data.IOLock && !Mod_Data.autoPlay && !Mod_Data.winErrorLock && !Mod_Data.creditErrorLock)
            //    {
            //        Mod_Data.Bet = Mod_Data.BetOri;
            //        Mod_Data.odds = Mod_Data.maxOdds;
            //        if (!Mod_Data.afterBonus) m_SlotMediatorController.SendMessage("m_state", "StopAllGameFrame");
            //        else Mod_Data.Win = 0;
            //        m_SlotMediatorController.SendMessage("m_state", "UpdateUIscore");

            //    }
            //}
            //+注
            if (Input.GetKeyDown(KeyCode.Z) && !Mod_Data.autoPlay)
            {
                Debug.Log("+D");
                Mod_Data.credit += 100;
                m_SlotMediatorController.SendMessage("m_state", "UpdateUIscore");
            }
            ////統計資料
            //if (Input.GetKeyDown(KeyCode.O) && !Mod_Data.autoPlay)
            //{  
            //    if (!Mod_Data.autoPlay && !Mod_Data.winErrorLock && !Mod_Data.creditErrorLock && !Mod_Data.IOLock)
            //    {

            //        Mod_Data.IOLock = true;
            //        if (!Mod_Data.afterBonus) m_SlotMediatorController.SendMessage("m_state", "StopAllGameFrame");
            //        m_SlotMediatorController.SendMessage("m_state", "OpenAccount");
            //    }
            //}
            ////後台
            //if (Input.GetKeyDown(KeyCode.P) && !Mod_Data.autoPlay)
            //{  
            //    if (!Mod_Data.autoPlay && !Mod_Data.winErrorLock && !Mod_Data.creditErrorLock && !Mod_Data.IOLock)
            //    {
            //        Mod_Data.IOLock = true;
            //        if (!Mod_Data.afterBonus) m_SlotMediatorController.SendMessage("m_state", "StopAllGameFrame");
            //        m_SlotMediatorController.SendMessage("m_state", "OpenLogin");
            //    }
            //}
            ////洗分
            //if (Input.GetKeyDown(KeyCode.E) && !Mod_Data.autoPlay)
            //{  
            //        m_SlotMediatorController.SendMessage("m_state", "CheckClearPoint");
            //        Mod_Data.Win = 0;
            //        m_SlotMediatorController.SendMessage("m_state", "UpdateUIscore");
            //    Debug.Log("clear");
            //}
            //開分
            //if (Input.GetKeyDown(KeyCode.W) && !Mod_Data.autoPlay)
            //{  //
            //        m_SlotMediatorController.SendMessage("m_state", "OpenPoint");
            //}


            //-----賽菲LED控制
            SephirothOneButtonLed(0, 1);
            SephirothOneButtonLed(2, 1);
            SephirothOneButtonLed(3, 0);
            SephirothOneButtonLed(4, 0);
            SephirothOneButtonLed(5, 0);
            SephirothOneButtonLed(6, 1);
            SephirothOneButtonLed(7, 1);
            SephirothOneButtonLed(8, 1);

        }

        CheckClearPoint();
        hardSpaceButtonDown = false;
    }
    public override void Exit()
    {
        Mod_Data.afterBonus = false;
        Mod_Data.inBaseSpin = false;
        if (BillAcceptorSettingData.BillOpenClose)
        {
            BillAcceptorSettingData.BillAcceptorEnable = false;
            BillAcceptorSettingData.GetOrderType = "BillEnableDisable";
        }
        // Debug.Log("BaseSpinExit");
        base.Exit();
    }

    public override void SpecialOrder()
    {
    }

    public void SephirothButton(int ButtonNumber)
    {
        switch (ButtonNumber)
        {
            case 0:  //最大押注
                if (!Mod_Data.IOLock && !Mod_Data.autoPlay && !Mod_Data.winErrorLock && !Mod_Data.creditErrorLock && !Mod_Data.monthLock && !Mod_Data.billLock && !Mod_Data.memberLcok)
                {
                    Mod_Data.Bet = Mod_Data.BetOri;
                    Mod_Data.odds = Mod_Data.maxOdds;
                    if (!Mod_Data.afterBonus) m_SlotMediatorController.SendMessage("m_state", "StopAllGameFrame");
                    else Mod_Data.Win = 0;
                    m_SlotMediatorController.SendMessage("m_state", "UpdateUIscore");

                }
                break;
            case 2: //自動
                if (!Mod_Data.IOLock && !Mod_Data.winErrorLock && !Mod_Data.creditErrorLock && !Mod_Data.monthLock && !Mod_Data.billLock && !Mod_Data.memberLcok) Mod_Data.autoPlay = !Mod_Data.autoPlay;
                break;
            case 3: //停1

                break;
            case 4: //停2

                break;
            case 5: //停3

                break;
            case 6: //停4 押注-
                if (!Mod_Data.IOLock && !Mod_Data.autoPlay && !Mod_Data.winErrorLock && !Mod_Data.creditErrorLock && !Mod_Data.monthLock && !Mod_Data.billLock && !Mod_Data.memberLcok)
                {
                    Mod_Data.Bet = Mod_Data.BetOri;
                    if (Mod_Data.odds > 1)
                    {
                        Mod_Data.odds--;
                        if (!Mod_Data.afterBonus) m_SlotMediatorController.SendMessage("m_state", "StopAllGameFrame");
                        else Mod_Data.Win = 0;
                        m_SlotMediatorController.SendMessage("m_state", "UpdateUIscore");
                    }
                }
                break;
            case 7: //停5 押注+
                if (!Mod_Data.IOLock && !Mod_Data.autoPlay && !Mod_Data.winErrorLock && !Mod_Data.creditErrorLock && !Mod_Data.monthLock && !Mod_Data.billLock && !Mod_Data.memberLcok)
                {
                    Mod_Data.Bet = Mod_Data.BetOri;
                    if (Mod_Data.odds < Mod_Data.maxOdds)
                    {
                        Mod_Data.odds++;
                        if (!Mod_Data.afterBonus) m_SlotMediatorController.SendMessage("m_state", "StopAllGameFrame");
                        else Mod_Data.Win = 0;
                        m_SlotMediatorController.SendMessage("m_state", "UpdateUIscore");
                    }

                }
                break;
            case 8: //開始 全停 得分
                if (!Mod_Data.IOLock && !Mod_Data.autoPlay && !Mod_Data.winErrorLock && !Mod_Data.creditErrorLock && !Mod_Data.monthLock && !Mod_Data.billLock && !Mod_Data.memberLcok)
                {
                    hardSpaceButtonDown = true;
                }
                break;
            case 9: //側面前方按鈕 開分
                if (!Mod_Data.IOLock && !Mod_Data.autoPlay && !Mod_Data.winErrorLock && !Mod_Data.creditErrorLock && !Mod_Data.monthLock && !Mod_Data.billLock && Mod_Data.Win <= 0 && !Mod_Data.memberLcok)
                {
                    m_SlotMediatorController.SendMessage("m_state", "OpenPoint");
                    Debug.Log("OPENPOINT");
                }
                break;
            case 10: //側面後方按鈕 洗分
                //Mod_Data.credit -= 500;
                //if (Mod_Data.credit < 0) Mod_Data.credit = 0;
                //m_SlotMediatorController.SendMessage("m_state", "UpdateUIscore");
                break;
            case 12: //統計資料
                if (!Mod_Data.autoPlay && !Mod_Data.winErrorLock && !Mod_Data.creditErrorLock && !Mod_Data.IOLock)
                {

                    Mod_Data.IOLock = true;
                    if (!Mod_Data.afterBonus) m_SlotMediatorController.SendMessage("m_state", "StopAllGameFrame");
                    m_SlotMediatorController.SendMessage("m_state", "OpenAccount");
                }
                break;
            case 13: //設定
                if (!Mod_Data.autoPlay && !Mod_Data.winErrorLock && !Mod_Data.creditErrorLock && !Mod_Data.IOLock)
                {
                    Mod_Data.IOLock = true;
                    if (!Mod_Data.afterBonus) m_SlotMediatorController.SendMessage("m_state", "StopAllGameFrame");
                    m_SlotMediatorController.SendMessage("m_state", "OpenLogin");
                }
                break;

        }
    }

    public void SephirothOneButtonLed(int ButtonNumber, byte SwitchLed)
    {
        Status = (CFPGADrvBridge.STATUS)CFPGADrvBridge.FD_SetGPOByIndex(0, (BYTE)ButtonNumber, SwitchLed);
    }

    public void CheckClearPoint()
    {

        if (!Mod_Data.autoPlay && !Mod_Data.IOLock && (Mod_Data.Win <= 0 || Mod_Data.creditErrorLock || Mod_Data.winErrorLock || Mod_Data.monthLock) && !Mod_OpenClearPoint.ClearLessRuning)
        {
            Status = (CFPGADrvBridge.STATUS)CFPGADrvBridge.FD_GetGPIByIndex(0, (BYTE)10, ref DataByte);
            if (DataByte == 0)
            {
                ClearButtonHoldTime += Time.deltaTime;
                if (ClearButtonHoldTime >= 3f)
                {
                    m_SlotMediatorController.SendMessage("m_state", "CheckClearPoint");
                    ClearButtonHoldTime = 0f;
                    Mod_Data.Win = 0;
                    m_SlotMediatorController.SendMessage("m_state", "UpdateUIscore");
                }
            }
            else
            {
                ClearButtonHoldTime = 0;
            }
        }

    }
}


//滾輪啟動
public class BaseScrolling : Mod_State
{
    CFPGADrvBridge.STATUS Status = new CFPGADrvBridge.STATUS(); //賽菲
    byte DataByte = 1;
    bool[] ButtonClickLong = new bool[32];
    public BaseScrolling() : base()
    {

        stateName = STATE.BaseScrolling;
        // base.Enter();
    }

    float detectButtonDownTime = 0;

    public override void Enter()
    {
        if (BillAcceptorSettingData.BillOpenClose) BillAcceptorSettingData.CheckIsInBaseSpin = true;
        //若不正常啟動時 斷電恢復
        if (Mod_Data.StartNotNormal)
        {
            detectButtonDownTime += Time.deltaTime;
            if (detectButtonDownTime > 2f)
            {
                m_SlotMediatorController.SendMessage("m_state", "SetReel");
                m_SlotMediatorController.SendMessage("m_state", "CheckBonus");
                m_SlotMediatorController.SendMessage("m_state", "GameMathCount");
                m_SlotMediatorController.SendMessage("m_state", "GetLocalGameRound");
                m_SlotMediatorController.SendMessage("m_state", "ServerWork", (int)Mod_Client_Data.messagetype.gamehistory);
                m_SlotMediatorController.SendMessage("m_state", "SaveLocalGameRound");

                m_SlotMediatorController.SendMessage("m_state", "StartRunSlots");
                detectButtonDownTime = 0;
                BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.status, 1);
                base.Enter();
            }
        }
        else
        {
            m_SlotMediatorController.SendMessage("m_state", "StartRunSlots");
            BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.status, 1);
            base.Enter();
        }

    }


    public override void Update()
    {
        detectButtonDownTime += Time.deltaTime;

        if (detectButtonDownTime > 0.2f)
        {
            nextState = new BaseEnd();
            nextState.setMediator(m_SlotMediatorController);
            stage = EVENT.EXIT;
        }

        SephirothOneButtonLed(0, 0);
        SephirothOneButtonLed(2, 1);
        SephirothOneButtonLed(3, 1);
        SephirothOneButtonLed(4, 1);
        SephirothOneButtonLed(5, 1);
        SephirothOneButtonLed(6, 1);
        SephirothOneButtonLed(7, 1);
        SephirothOneButtonLed(8, 1);

    }


    public override void Exit()
    {
        base.Exit();

    }
    public override void SpecialOrder()
    {
        // Debug.Log("TestBaseEnd");
    }

    public void SephirothOneButtonLed(int ButtonNumber, byte SwitchLed)
    {
        Status = (CFPGADrvBridge.STATUS)CFPGADrvBridge.FD_SetGPOByIndex(0, (BYTE)ButtonNumber, SwitchLed);
    }

}

public class BaseEnd : Mod_State
{
    CFPGADrvBridge.STATUS Status = new CFPGADrvBridge.STATUS(); //賽菲
    byte DataByte = 1;
    bool[] ButtonClickLong = new bool[32];
    public BaseEnd() : base()
    {

        stateName = STATE.BaseEnd;
        // base.Enter();
    }
    bool isSpaceOnce = false;

    public override void Enter()
    {

        m_SlotMediatorController.SendMessage("m_state", "OpenBlankButton");
        isSpaceOnce = false;
        base.Enter();

    }

    bool hardSpaceButtonDown = false;
    public override void Update()
    {
        for (int i = 0; i < 32; i++)
        {
            Status = (CFPGADrvBridge.STATUS)CFPGADrvBridge.FD_GetGPIByIndex(0, (BYTE)i, ref DataByte);
            if (DataByte == 0)
            {
                if (!ButtonClickLong[i])
                {
                    ButtonClickLong[i] = true;
                    SephirothButton(i);
                }

            }

            else if (DataByte != 0)
            {
                ButtonClickLong[i] = false;
            }

        }
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Space))
        {
            hardSpaceButtonDown = true;
        }
#endif

        #region 勝圖硬體<-------------------
        read_long_result1 = qxt_dio_readdword(0);  //讀取輸入訊號
        read_long_result2 = qxt_dio_readdword(4);
        if (qxt_dio_readword(1) != 255) { print(qxt_dio_readword(1)); }
        if (read_long_result1 != read_long_result_stored1)
        {
            switch (read_long_result1)
            {   //按鈕順序由左至右
                case 0xFFFFFFFB:
                    print("出票");//停一
                    break;
                case 0xFFFFFFFD:
                    print("服務鈴");//停二
                    break;
                case 0xFFDFFFFF:
                    print("押注1");//停三 
                    if (!Mod_TimeController.GamePasue && !Mod_Data.MachineError) GameObject.Find("Slots").GetComponent<Slots>().BlankButtonClick(0);
                    break;
                case 0xFFBFFFFF:
                    print("押注2");//停四 -
                    if (!Mod_TimeController.GamePasue && !Mod_Data.MachineError) GameObject.Find("Slots").GetComponent<Slots>().BlankButtonClick(1);
                    break;
                case 0xFF7FFFFF:
                    print("押注3");//停五 +
                    if (!Mod_TimeController.GamePasue && !Mod_Data.MachineError) GameObject.Find("Slots").GetComponent<Slots>().BlankButtonClick(2);
                    break;
                case 0xFEFFFFFF:
                    print("押注4"); // most
                    if (!Mod_TimeController.GamePasue && !Mod_Data.MachineError) GameObject.Find("Slots").GetComponent<Slots>().BlankButtonClick(3);
                    break;
                case 0xFDFFFFFF:
                    print("押注5");//auto
                    if (!Mod_TimeController.GamePasue && !Mod_Data.MachineError) GameObject.Find("Slots").GetComponent<Slots>().BlankButtonClick(4);
                    break;
                case 0xFFFFFFFE:
                    print("開始");
                    if (!Mod_TimeController.GamePasue && !Mod_Data.MachineError) hardSpaceButtonDown = true;
                    break;
                case 0x7FFFFFFF:     //轉鑰匙時的訊號 開啟後台用
                    break;
            }
            read_long_result_stored1 = qxt_dio_readdword(0);
        }

        #endregion

        if ((hardSpaceButtonDown) && !Mod_Data.MachineError)
        {
            Mod_Data.BlankClick = false;
            if (!isSpaceOnce)
                m_SlotMediatorController.SendMessage("m_state", "StopRunSlots");

            isSpaceOnce = true;
        }
        if (Input.GetKeyDown(KeyCode.A) && !Mod_TimeController.GamePasue && !Mod_Data.MachineError)
        {  //自動
            Mod_Data.autoPlay = !Mod_Data.autoPlay;
        }

        if (Mod_Data.reelAllStop)
        {
            Debug.Log("StopAll");
            m_SlotMediatorController.SendMessage("m_state", "StopRunSlots");
            m_SlotMediatorController.SendMessage("m_state", "PlayAnimation");
            nextState = new BaseRollScore();
            nextState.setMediator(m_SlotMediatorController);
            stage = EVENT.EXIT;
        }


        SephirothOneButtonLed(0, 0);
        SephirothOneButtonLed(2, 1);
        SephirothOneButtonLed(3, 1);
        SephirothOneButtonLed(4, 1);
        SephirothOneButtonLed(5, 1);
        SephirothOneButtonLed(6, 1);
        SephirothOneButtonLed(7, 1);
        SephirothOneButtonLed(8, 1);


        hardSpaceButtonDown = false;
    }


    public override void Exit()
    {
        base.Exit();
    }
    public override void SpecialOrder()
    {
        // Debug.Log("TestBaseEnd");
    }

    public void SephirothButton(int ButtonNumber)
    {
        switch (ButtonNumber)
        {
            case 2: //自動
                Mod_Data.autoPlay = !Mod_Data.autoPlay;
                break;
            case 3: //停1
                GameObject.Find("Slots").GetComponent<Slots>().BlankButtonClick(0);
                break;
            case 4: //停2
                GameObject.Find("Slots").GetComponent<Slots>().BlankButtonClick(1);
                break;
            case 5: //停3
                GameObject.Find("Slots").GetComponent<Slots>().BlankButtonClick(2);
                break;
            case 6: //停4 
                GameObject.Find("Slots").GetComponent<Slots>().BlankButtonClick(3);
                break;
            case 7: //停5 
                GameObject.Find("Slots").GetComponent<Slots>().BlankButtonClick(4);
                break;
            case 8: //全停 得分
                hardSpaceButtonDown = true;
                break;
        }
    }
    public void SephirothOneButtonLed(int ButtonNumber, byte SwitchLed)
    {
        Status = (CFPGADrvBridge.STATUS)CFPGADrvBridge.FD_SetGPOByIndex(0, (BYTE)ButtonNumber, SwitchLed);
    }
}

public class BaseRollScore : Mod_State
{

    CFPGADrvBridge.STATUS Status = new CFPGADrvBridge.STATUS(); //賽菲
    byte DataByte = 1;
    bool[] ButtonClickLong = new bool[32];

    public BaseRollScore() : base()
    {

        stateName = STATE.BaseRollScore;

    }
    float timer = 0;
    public override void Enter()
    {
        //Status = (CFPGADrvBridge.STATUS)CFPGADrvBridge.FpgaPic_Init(); //賽菲
        m_SlotMediatorController.SendMessage("m_state", "OpenBlankButton");

        timer += Time.deltaTime;
        if (timer > 0.2f)
        {
            if (Mod_Data.getBonus)
            {
                nextState = new BonusTransIn();
                Mod_Data.getBonus = false;
            }
            else
            {
                nextState = new BaseSpin();
                Mod_Data.credit += Mod_Data.Win * Mod_Data.Denom;//獲得bonus先不加入彩分
            }

            nextState.setMediator(m_SlotMediatorController);
            if (Mod_Data.Pay > 0)
            {
                Mod_Data.runScore = true;
                m_SlotMediatorController.SendMessage("m_state", "StartFastRollScore");
                Debug.Log("RollScoreEnter" + Mod_Data.runScore);
            }
            timer = 0;
            base.Enter();
        }


    }
    bool hardSpaceButtonDown = false, stopScore = false;
    public override void Update()
    {
        for (int i = 0; i < 32; i++)
        {
            Status = (CFPGADrvBridge.STATUS)CFPGADrvBridge.FD_GetGPIByIndex(0, (BYTE)i, ref DataByte);
            if (DataByte == 0 && !ButtonClickLong[i])
            {

                ButtonClickLong[i] = true;
                SephirothButton(i);
            }
            else if (DataByte != 0)
            {
                ButtonClickLong[i] = false;
            }
        }
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Space))
        {
            hardSpaceButtonDown = true;
        }
#endif

        #region 勝圖硬體<-------------------
        read_long_result1 = qxt_dio_readdword(0);  //讀取輸入訊號
        read_long_result2 = qxt_dio_readdword(4);
        if (qxt_dio_readword(1) != 255) { print(qxt_dio_readword(1)); }
        if (read_long_result1 != read_long_result_stored1)
        {
            switch (read_long_result1)
            {   //按鈕順序由左至右
                case 0xFFFFFFFB:
                    print("出票");//停一
                    break;
                case 0xFFFFFFFD:
                    print("服務鈴");//停二
                    break;
                case 0xFFDFFFFF:
                    print("押注1");//停三 
                    break;
                case 0xFFBFFFFF:
                    print("押注2");//停四 -
                    break;
                case 0xFF7FFFFF:
                    print("押注3");//停五 +
                    break;
                case 0xFEFFFFFF:
                    print("押注4"); // most
                    break;
                case 0xFDFFFFFF:
                    print("押注5");//auto
                    break;
                case 0xFFFFFFFE:
                    print("開始");
                    if (!Mod_TimeController.GamePasue && !Mod_Data.MachineError) hardSpaceButtonDown = true;
                    break;
                case 0x7FFFFFFF:     //轉鑰匙時的訊號 開啟後台用
                    break;
            }
            read_long_result_stored1 = qxt_dio_readdword(0);
        }

        #endregion

        timer += Time.deltaTime;

        if (Mod_Data.Win > 0)
        {
            if (Mod_Data.runScore)
            {
                if ((hardSpaceButtonDown || Mod_Data.BlankClick) && !Mod_TimeController.GamePasue && !Mod_Data.MachineError)
                {
                    if (timer > Mod_Data.BonusDelayTimes)
                    {
                        Mod_Data.BlankClick = false;
                        m_SlotMediatorController.SendMessage("m_state", "StopRollScore");
                        stage = EVENT.EXIT;
                    }
                }
            }
            else
            {
                if (!stopScore)
                {
                    m_SlotMediatorController.SendMessage("m_state", "StopRollScore");
                    stopScore = true;
                    stage = EVENT.EXIT;
                }
            }
        }
        else
        {
            stage = EVENT.EXIT;
        }

        //賽菲
        SephirothOneButtonLed(0, 0);
        SephirothOneButtonLed(2, 0);
        SephirothOneButtonLed(3, 0);
        SephirothOneButtonLed(4, 0);
        SephirothOneButtonLed(5, 0);
        SephirothOneButtonLed(6, 0);
        SephirothOneButtonLed(7, 0);
        SephirothOneButtonLed(8, 1);

    }

    public override void Exit()
    {
        Mod_Data.runScore = false;
        Mod_Data.StartNotNormal = false;
        base.Exit();

    }
    public override void SpecialOrder()
    {

    }

    public void SephirothButton(int ButtonNumber)
    {
        switch (ButtonNumber)
        {
            case 2: //自動

                break;
            case 3: //停1

                break;
            case 4: //停2

                break;
            case 5: //停3

                break;
            case 6: //停4 

                break;
            case 7: //停5 

                break;
            case 8: //全停 得分
                hardSpaceButtonDown = true;
                break;
        }
    }
    public void SephirothOneButtonLed(int ButtonNumber, byte SwitchLed)
    {
        Status = (CFPGADrvBridge.STATUS)CFPGADrvBridge.FD_SetGPOByIndex(0, (BYTE)ButtonNumber, SwitchLed);
    }

}

public class BonusTransIn : Mod_State
{
    CFPGADrvBridge.STATUS Status = new CFPGADrvBridge.STATUS(); //賽菲
    byte DataByte = 1;
    bool[] ButtonClickLong = new bool[32];

    float timer = 0;
    public BonusTransIn() : base()
    {

        stateName = STATE.BonustransIn;

    }

    public override void Enter()
    {
        if (Mod_Data.StartNotNormal)
        {
            timer += Time.deltaTime;
            if (timer > 0.5f)
            {
                m_SlotMediatorController.SendMessage("m_state", "PlayBonusTransAnim");
                Mod_Data.BlankClick = false;
                BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.status, 2);
                base.Enter();
            }
        }
        else
        {
            timer += Time.deltaTime;
            if (timer > 2f)
            {
                m_SlotMediatorController.SendMessage("m_state", "PlayBonusTransAnim");
                Mod_Data.BlankClick = false;
                BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.status, 2);
                base.Enter();
            }
        }


    }
    bool hardSpaceButtonDown = false;
    public override void Update()
    {
        for (int i = 0; i < 32; i++)
        {
            Status = (CFPGADrvBridge.STATUS)CFPGADrvBridge.FD_GetGPIByIndex(0, (BYTE)i, ref DataByte);
            if (DataByte == 0 && !ButtonClickLong[i])
            {

                ButtonClickLong[i] = true;
                SephirothButton(i);
            }
            else if (DataByte != 0)
            {
                ButtonClickLong[i] = false;
            }
        }

#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Space))
        {
            hardSpaceButtonDown = true;
        }
#endif

        #region 勝圖硬體<-------------------
        read_long_result1 = qxt_dio_readdword(0);  //讀取輸入訊號
        read_long_result2 = qxt_dio_readdword(4);
        if (qxt_dio_readword(1) != 255) { print(qxt_dio_readword(1)); }
        if (read_long_result1 != read_long_result_stored1)
        {
            switch (read_long_result1)
            {   //按鈕順序由左至右
                case 0xFFFFFFFB:
                    print("出票");//停一
                    break;
                case 0xFFFFFFFD:
                    print("服務鈴");//停二
                    break;
                case 0xFFDFFFFF:
                    print("押注1");//停三 
                    break;
                case 0xFFBFFFFF:
                    print("押注2");//停四 -
                    break;
                case 0xFF7FFFFF:
                    print("押注3");//停五 +
                    break;
                case 0xFEFFFFFF:
                    print("押注4"); // most
                    break;
                case 0xFDFFFFFF:
                    print("押注5");//auto
                    break;
                case 0xFFFFFFFE:
                    print("開始");
                    if (!Mod_TimeController.GamePasue && !Mod_Data.MachineError) hardSpaceButtonDown = true;
                    break;
                case 0x7FFFFFFF:     //轉鑰匙時的訊號 開啟後台用
                    break;
            }
            read_long_result_stored1 = qxt_dio_readdword(0);
        }

        #endregion

        if (Mod_Data.transInAnimEnd)
        {
            // m_SlotMediatorController.SendMessage(this, "ShowChoosePanel");//如果還有可玩場數 就跳選擇畫面
            //
            //Mod_Data.startBonus = true;
            if (timer < 3) timer += Time.deltaTime;

            if (!Mod_Data.BonusSwitch)
            {
                m_SlotMediatorController.SendMessage("m_state", "ShowStartTriggerPanel"); //顯示贏得幾場
                m_SlotMediatorController.SendMessage("m_state", "StopAllGameFrame"); //停止所有框線
                Mod_Data.BonusSwitch = true;
                m_SlotMediatorController.SendMessage("m_state", "ChangeScene", 0);//轉換場景至Bonus
            }

            if (timer > 2)
            {
                m_SlotMediatorController.SendMessage("m_state", "OpenBlankButton");
            }

            if ((hardSpaceButtonDown || Mod_Data.BlankClick) && timer > 3 && !Mod_Data.MachineError)
            {
                Mod_Data.BlankClick = false;
                m_SlotMediatorController.SendMessage("m_state", "CloseTriggerPanel");
                m_SlotMediatorController.SendMessage("m_state", "BonustransEnd");
                Mod_Data.transInAnimEnd = false;
                // Mod_Data.startBonus = true;
                timer = 0;
            }
        }

        if (Mod_Data.startBonus)
        {
            timer += Time.deltaTime;

            if (timer > 1)
            {
                Mod_Data.BonusIsPlayedCount = 0;
                Mod_Data.startBonus = false;
                nextState = new BonusSpin();
                nextState.setMediator(m_SlotMediatorController);
                Mod_Data.BonusSwitch = true;
                stage = EVENT.EXIT;
            }
        }
        hardSpaceButtonDown = false;
        ButtonClickLong[8] = false;
    }
    public override void Exit()
    {
        //m_SlotMediatorController.SendMessage(this, "BonustransEnd");
        Debug.Log("BonusTransExit");
        base.Exit();
    }
    public override void SpecialOrder()
    {
        //  Debug.Log("TestBonusTrans");
    }
    public void SephirothButton(int ButtonNumber)
    {
        switch (ButtonNumber)
        {
            case 2: //自動

                break;
            case 3: //停1

                break;
            case 4: //停2

                break;
            case 5: //停3

                break;
            case 6: //停4 

                break;
            case 7: //停5 

                break;
            case 8: //全停 得分
                hardSpaceButtonDown = true;
                break;
        }
    }
    public void SephirothOneButtonLed(int ButtonNumber, byte SwitchLed)
    {
        Status = (CFPGADrvBridge.STATUS)CFPGADrvBridge.FD_SetGPOByIndex(0, (BYTE)ButtonNumber, SwitchLed);
    }
}

public class BonusSpin : Mod_State
{
    CFPGADrvBridge.STATUS Status = new CFPGADrvBridge.STATUS(); //賽菲
    float timer = 0;
    public BonusSpin() : base()
    {

        stateName = STATE.BonusSpin;
    }

    public override void Enter()
    {
        timer += Time.deltaTime;
        if (timer > 0.5f)
        {
            Mod_Data.BonusIsPlayedCount++;
            Mod_Data.StartNotNormal = false;
            base.Enter();
        }
    }
    public override void Update()
    {
        Debug.Log(Mod_Data.BonusSwitch);
        Debug.Log("BonusSpinUpdate");

        m_SlotMediatorController.SendMessage("m_state", "UpdateUIscore");
        m_SlotMediatorController.SendMessage("m_state", "SetReel");
        m_SlotMediatorController.SendMessage("m_state", "CheckBonus");
        m_SlotMediatorController.SendMessage("m_state", "GameMathCount");
        m_SlotMediatorController.SendMessage("m_state", "ServerWork", (int)Mod_Client_Data.messagetype.gamehistory);
        m_SlotMediatorController.SendMessage("m_state", "SaveData");


        nextState = new BonusScrolling();
        nextState.setMediator(m_SlotMediatorController);
        stage = EVENT.EXIT;

        //賽菲
        SephirothOneButtonLed(0, 0);
        SephirothOneButtonLed(2, 0);
        SephirothOneButtonLed(3, 0);
        SephirothOneButtonLed(4, 0);
        SephirothOneButtonLed(5, 0);
        SephirothOneButtonLed(6, 0);
        SephirothOneButtonLed(7, 0);
        SephirothOneButtonLed(8, 0);
    }
    public override void Exit()
    {

        Debug.Log("BonusSpinExit");
        base.Exit();
    }
    public override void SpecialOrder()
    {
        // Debug.Log("TestBonusSpin");
    }
    public void SephirothOneButtonLed(int ButtonNumber, byte SwitchLed)
    {
        Status = (CFPGADrvBridge.STATUS)CFPGADrvBridge.FD_SetGPOByIndex(0, (BYTE)ButtonNumber, SwitchLed);
    }

}
public class BonusScrolling : Mod_State
{
    CFPGADrvBridge.STATUS Status = new CFPGADrvBridge.STATUS(); //賽菲
    byte DataByte = 1;
    bool[] ButtonClickLong = new bool[32];
    public BonusScrolling() : base()
    {

        stateName = STATE.BonusScrolling;
        // base.Enter();
    }

    float detectButtonDownTime = 0;

    public override void Enter()
    {
        if (Mod_Data.StartNotNormal)
        {
            detectButtonDownTime += Time.deltaTime;

            if (detectButtonDownTime > 2f)
            {
                m_SlotMediatorController.SendMessage("m_state", "SetReel");
                m_SlotMediatorController.SendMessage("m_state", "CheckBonus");
                m_SlotMediatorController.SendMessage("m_state", "GameMathCount");
                m_SlotMediatorController.SendMessage("m_state", "GetLocalGameRound");
                m_SlotMediatorController.SendMessage("m_state", "ServerWork", (int)Mod_Client_Data.messagetype.gamehistory);
                m_SlotMediatorController.SendMessage("m_state", "SaveLocalGameRound");

                m_SlotMediatorController.SendMessage("m_state", "StartRunSlots");
                m_SlotMediatorController.SendMessage("m_state", "PlayBonusBackGroundSound");
                detectButtonDownTime = 0;
                BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.status, 3);
                base.Enter();
            }
        }
        else
        {
            m_SlotMediatorController.SendMessage("m_state", "StartRunSlots");
            BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.status, 3);
            base.Enter();
        }

    }


    public override void Update()
    {
        detectButtonDownTime += Time.deltaTime;

        if (detectButtonDownTime > 0.2f)
        {
            nextState = new BonusEnd();
            nextState.setMediator(m_SlotMediatorController);
            stage = EVENT.EXIT;
        }

        //賽菲
        SephirothOneButtonLed(0, 0);
        SephirothOneButtonLed(2, 0);
        SephirothOneButtonLed(3, 0);
        SephirothOneButtonLed(4, 0);
        SephirothOneButtonLed(5, 0);
        SephirothOneButtonLed(6, 0);
        SephirothOneButtonLed(7, 0);
        SephirothOneButtonLed(8, 0);

    }

    public void SephirothOneButtonLed(int ButtonNumber, byte SwitchLed)
    {
        Status = (CFPGADrvBridge.STATUS)CFPGADrvBridge.FD_SetGPOByIndex(0, (BYTE)ButtonNumber, SwitchLed);
    }

    public override void Exit()
    {
        base.Exit();

    }
    public override void SpecialOrder()
    {
        // Debug.Log("TestBaseEnd");
    }
}
public class BonusEnd : Mod_State
{

    public BonusEnd() : base()
    {
        stateName = STATE.BonusEnd;
    }

    public override void Enter()
    {
        Debug.Log("BonusEndEnter");
        // m_SlotMediatorController.SendMessage("m_state", "OpenBlankButton");

        base.Enter();

    }
    public override void Update()
    {
        //Debug.Log("BonusEndUpdate");
        if (Mod_Data.reelAllStop)
        {
            m_SlotMediatorController.SendMessage("m_state", "StopRunSlots");
            //m_SlotMediatorController.SendMessage("m_state", "CheckBonus");
            //m_SlotMediatorController.SendMessage("m_state", "GameMathCount");
            Mod_Data.BonusDelayTimes = 0;

            m_SlotMediatorController.SendMessage("m_state", "PlayDKSpecialAnim");
            m_SlotMediatorController.SendMessage("m_state", "PlayAnimation");

            nextState = new BonusRollScore();
            nextState.setMediator(m_SlotMediatorController);
            stage = EVENT.EXIT;
        }
    }
    public override void Exit()
    {

        Debug.Log("BonusEndExit");
        base.Exit();
    }
    public override void SpecialOrder()
    {
        // Debug.Log("TestBonusEnd");
    }
}
public class BonusRollScore : Mod_State
{

    CFPGADrvBridge.STATUS Status = new CFPGADrvBridge.STATUS(); //賽菲
    byte DataByte = 1;
    bool[] ButtonClickLong = new bool[32];

    public BonusRollScore() : base()
    {

        stateName = STATE.BonusRollScore;

    }
    float timer = 0;
    public override void Enter()
    {
        //Status = (CFPGADrvBridge.STATUS)CFPGADrvBridge.FpgaPic_Init(); //賽菲
        m_SlotMediatorController.SendMessage("m_state", "OpenBlankButton");

        timer += Time.deltaTime;
        if (timer > 0.7)
        {
            if (Mod_Data.BonusIsPlayedCount >= Mod_Data.BonusCount)
            {
                nextState = new BonusTransOut();
                Debug.Log("A");
            }
            else
            {
                if (Mod_Data.getBonus)
                {

                    nextState = new GetBonusInBonus();
                    Mod_Data.getBonus = false;
                }
                else
                {
                    nextState = new BonusSpin();
                }
            }
            nextState.setMediator(m_SlotMediatorController);
            if (Mod_Data.Pay > 0)
            {
                Mod_Data.runScore = true;
                m_SlotMediatorController.SendMessage("m_state", "StartFastRollScore");
                Debug.Log("RollScoreEnter" + Mod_Data.runScore);
                Debug.Log("Mod_Data.Win" + Mod_Data.Win);
            }
            // m_SlotMediatorController.SendMessage("m_state", "SaveData");

            timer = 0;
            base.Enter();
        }


    }
    bool hardSpaceButtonDown = false, stopScore = false;
    public override void Update()
    {
        for (int i = 0; i < 32; i++)
        {
            Status = (CFPGADrvBridge.STATUS)CFPGADrvBridge.FD_GetGPIByIndex(0, (BYTE)i, ref DataByte);
            if (DataByte == 0 && !ButtonClickLong[i])
            {

                ButtonClickLong[i] = true;
                SephirothButton(i);
            }
            else if (DataByte != 0)
            {
                ButtonClickLong[i] = false;
            }
        }
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Space))
        {
            hardSpaceButtonDown = true;
        }
#endif

        #region 勝圖硬體<-------------------
        read_long_result1 = qxt_dio_readdword(0);  //讀取輸入訊號
        read_long_result2 = qxt_dio_readdword(4);
        if (qxt_dio_readword(1) != 255) { print(qxt_dio_readword(1)); }
        if (read_long_result1 != read_long_result_stored1)
        {
            switch (read_long_result1)
            {   //按鈕順序由左至右
                case 0xFFFFFFFB:
                    print("出票");//停一
                    break;
                case 0xFFFFFFFD:
                    print("服務鈴");//停二
                    break;
                case 0xFFDFFFFF:
                    print("押注1");//停三 
                    break;
                case 0xFFBFFFFF:
                    print("押注2");//停四 -
                    break;
                case 0xFF7FFFFF:
                    print("押注3");//停五 +
                    break;
                case 0xFEFFFFFF:
                    print("押注4"); // most
                    break;
                case 0xFDFFFFFF:
                    print("押注5");//auto
                    break;
                case 0xFFFFFFFE:
                    print("開始");
                    if (!Mod_TimeController.GamePasue && !Mod_Data.MachineError) hardSpaceButtonDown = true;
                    break;
                case 0x7FFFFFFF:     //轉鑰匙時的訊號 開啟後台用
                    break;
            }
            read_long_result_stored1 = qxt_dio_readdword(0);
        }

        #endregion

        timer += Time.deltaTime;

        if ((Mod_Data.BonusSwitch && Mod_Data.Pay > 0) || (!Mod_Data.BonusSwitch && Mod_Data.Win > 0))
        {
            if (Mod_Data.runScore)
            {
                if ((hardSpaceButtonDown || Mod_Data.BlankClick) && !Mod_Data.MachineError)
                {
                    if (timer > Mod_Data.BonusDelayTimes)
                    {
                        Mod_Data.BlankClick = false;
                        m_SlotMediatorController.SendMessage("m_state", "StopRollScore");
                        if (Mod_Animation.isPlayingDKSpecialAnim) m_SlotMediatorController.SendMessage("m_state", "StopDKSpecialAnim");
                        stage = EVENT.EXIT;
                    }
                }
            }
            else
            {
                if (!stopScore)
                {
                    m_SlotMediatorController.SendMessage("m_state", "StopRollScore");
                    stopScore = true;
                }
                if (timer > Mod_Data.BonusDelayTimes)
                {
                    if (!stopScore) m_SlotMediatorController.SendMessage("m_state", "StopRollScore");
                    if (Mod_Animation.isPlayingDKSpecialAnim) m_SlotMediatorController.SendMessage("m_state", "StopDKSpecialAnim");
                    stage = EVENT.EXIT;
                }
            }
        }
        else
        {
            if (timer > Mod_Data.BonusDelayTimes)
            {
                if (Mod_Animation.isPlayingDKSpecialAnim) m_SlotMediatorController.SendMessage("m_state", "StopDKSpecialAnim");
                stage = EVENT.EXIT;
            }
        }
        //賽菲
        SephirothOneButtonLed(0, 0);
        SephirothOneButtonLed(2, 0);
        SephirothOneButtonLed(3, 0);
        SephirothOneButtonLed(4, 0);
        SephirothOneButtonLed(5, 0);
        SephirothOneButtonLed(6, 0);
        SephirothOneButtonLed(7, 0);
        SephirothOneButtonLed(8, 1);

    }

    public override void Exit()
    {
        Mod_Data.runScore = false;
        base.Exit();

    }
    public override void SpecialOrder()
    {

    }

    public void SephirothButton(int ButtonNumber)
    {
        switch (ButtonNumber)
        {
            case 2: //自動

                break;
            case 3: //停1

                break;
            case 4: //停2

                break;
            case 5: //停3

                break;
            case 6: //停4 

                break;
            case 7: //停5 

                break;
            case 8: //全停 得分
                hardSpaceButtonDown = true;
                break;
        }
    }
    public void SephirothOneButtonLed(int ButtonNumber, byte SwitchLed)
    {
        Status = (CFPGADrvBridge.STATUS)CFPGADrvBridge.FD_SetGPOByIndex(0, (BYTE)ButtonNumber, SwitchLed);
    }

}
public class BonusTransOut : Mod_State
{

    public BonusTransOut() : base()
    {

        stateName = STATE.BonusTransOut;
    }

    public override void Enter()

    {
        //  m_SlotMediatorController.SendMessage("m_state", "OpenBlankButton");
        Debug.Log("BonusTransOutEnter");
        Debug.Log("Mod_Data.Win" + Mod_Data.Win);
        timer += Time.deltaTime;
        if (timer > 2)
        {
            m_SlotMediatorController.SendMessage("m_state", "PlayBonusTransOutAnim");
            timer = 0;

            base.Enter();
        }



    }
    float timer = 0;
    public override void Update()
    {

        if (Mod_Data.transInAnimEnd)
        {

            m_SlotMediatorController.SendMessage("m_state", "ShowBonusScorePanel");//如果可玩場次結束 就跳分數

            Mod_Data.BonusSpecialTimes = 1;
            Mod_Data.BonusDelayTimes = 0;
            nextState = new AfterBonusRollScore();
            nextState.setMediator(m_SlotMediatorController);
            Mod_Data.transInAnimEnd = false;
            timer = 10;
        }
        timer -= Time.deltaTime;
        if (timer > 0 && timer < 5)
        {
            stage = EVENT.EXIT;
        }

    }
    public override void Exit()
    {
        m_SlotMediatorController.SendMessage("m_state", "BonusEndtransOut");
        Mod_Data.BonusSwitch = false;
        m_SlotMediatorController.SendMessage("m_state", "StopAllGameFrame");
        m_SlotMediatorController.SendMessage("m_state", "ChangeScene", 1);
        Mod_Data.afterBonus = true;
        Debug.Log("BonusTransOutExit");
        base.Exit();
        //m_SlotMediatorController.SendMessage("m_state", "ChangeScene");
    }
    public override void SpecialOrder()
    {
        // Debug.Log("TestBonusEnd");
    }
}



public class AfterBonusRollScore : Mod_State
{

    CFPGADrvBridge.STATUS Status = new CFPGADrvBridge.STATUS(); //賽菲
    byte DataByte = 1;
    bool[] ButtonClickLong = new bool[32];

    public AfterBonusRollScore() : base()
    {

        stateName = STATE.AfterBonusRollScore;

    }
    float timer = 0;
    public override void Enter()
    {
        //Status = (CFPGADrvBridge.STATUS)CFPGADrvBridge.FpgaPic_Init(); //賽菲
        m_SlotMediatorController.SendMessage("m_state", "OpenBlankButton");

        timer += Time.deltaTime;

        if (timer > 0.7)
        {
            m_SlotMediatorController.SendMessage("m_state", "ComparisonMaxWinInBonus");
            nextState = new BaseSpin();
            nextState.setMediator(m_SlotMediatorController);
            Debug.Log("Mod_Data.Win" + Mod_Data.Win);
            Mod_Data.credit += Mod_Data.Win * Mod_Data.Denom;
            if (Mod_Data.Win > 0)
            {
                Mod_Data.runScore = true;
                m_SlotMediatorController.SendMessage("m_state", "StartRollScore");
                m_SlotMediatorController.SendMessage("m_state", "BannerRightAfterBonusShowOpen");
            }
            base.Enter();
        }


    }
    bool hardSpaceButtonDown = false, stopScore = false;
    public override void Update()
    {
        for (int i = 0; i < 32; i++)
        {
            Status = (CFPGADrvBridge.STATUS)CFPGADrvBridge.FD_GetGPIByIndex(0, (BYTE)i, ref DataByte);
            if (DataByte == 0 && !ButtonClickLong[i])
            {

                ButtonClickLong[i] = true;
                SephirothButton(i);
            }
            else if (DataByte != 0)
            {
                ButtonClickLong[i] = false;
            }
        }
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Space))
        {
            hardSpaceButtonDown = true;
        }
#endif

        #region 勝圖硬體<-------------------
        read_long_result1 = qxt_dio_readdword(0);  //讀取輸入訊號
        read_long_result2 = qxt_dio_readdword(4);
        if (qxt_dio_readword(1) != 255) { print(qxt_dio_readword(1)); }
        if (read_long_result1 != read_long_result_stored1)
        {
            switch (read_long_result1)
            {   //按鈕順序由左至右
                case 0xFFFFFFFB:
                    print("出票");//停一
                    break;
                case 0xFFFFFFFD:
                    print("服務鈴");//停二
                    break;
                case 0xFFDFFFFF:
                    print("押注1");//停三 
                    break;
                case 0xFFBFFFFF:
                    print("押注2");//停四 -
                    break;
                case 0xFF7FFFFF:
                    print("押注3");//停五 +
                    break;
                case 0xFEFFFFFF:
                    print("押注4"); // most
                    break;
                case 0xFDFFFFFF:
                    print("押注5");//auto
                    break;
                case 0xFFFFFFFE:
                    print("開始");
                    if (!Mod_TimeController.GamePasue && !Mod_Data.MachineError) hardSpaceButtonDown = true;
                    break;
                case 0x7FFFFFFF:     //轉鑰匙時的訊號 開啟後台用
                    break;
            }
            read_long_result_stored1 = qxt_dio_readdword(0);
        }

        #endregion

        if (Mod_Data.Win > 0)
        {
            if (Mod_Data.runScore)
            {
                if ((hardSpaceButtonDown || Mod_Data.BlankClick) && !Mod_Data.MachineError)
                {
                    if (timer > Mod_Data.BonusDelayTimes)
                    {
                        BackEnd_Data.SetDouble(BackEnd_Data.SramAccountData.totalWin, BackEnd_Data.GetDouble(BackEnd_Data.SramAccountData.totalWin) + Mod_Data.Win * Mod_Data.Denom);
                        BackEnd_Data.SetDouble(BackEnd_Data.SramAccountData.totalWin_Class, BackEnd_Data.GetDouble(BackEnd_Data.SramAccountData.totalWin_Class) + Mod_Data.Win * Mod_Data.Denom);
                        BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.winCount, BackEnd_Data.GetInt(BackEnd_Data.SramAccountData.winCount) + 1);
                        BackEnd_Data.SetInt(BackEnd_Data.SramAccountData.winCount_Class, BackEnd_Data.GetInt(BackEnd_Data.SramAccountData.winCount_Class) + 1);

                        Mod_Data.BlankClick = false;
                        m_SlotMediatorController.SendMessage("m_state", "StopRollScore");

                        stage = EVENT.EXIT;
                    }
                }
            }
            else
            {
                if (!stopScore)
                {
                    m_SlotMediatorController.SendMessage("m_state", "StopRollScore");
                    stopScore = true;
                    stage = EVENT.EXIT;
                }
            }
        }
        else
        {
            stage = EVENT.EXIT;
        }
        Mod_Data.BonusIsPlayedCount = 0;
        Mod_Data.BonusCount = 0;
        //賽菲
        SephirothOneButtonLed(0, 0);
        SephirothOneButtonLed(2, 0);
        SephirothOneButtonLed(3, 0);
        SephirothOneButtonLed(4, 0);
        SephirothOneButtonLed(5, 0);
        SephirothOneButtonLed(6, 0);
        SephirothOneButtonLed(7, 0);
        SephirothOneButtonLed(8, 1);

    }

    public override void Exit()
    {
        m_SlotMediatorController.SendMessage("m_state", "SaveData");
        m_SlotMediatorController.SendMessage("m_state", "ComparisonMaxWin");
        // m_SlotMediatorController.SendMessage("m_state", "BannerRightAfterBonusShowClose");
        Mod_Data.runScore = false;
        base.Exit();
    }
    public override void SpecialOrder()
    {

    }

    public void SephirothButton(int ButtonNumber)
    {
        switch (ButtonNumber)
        {
            case 2: //自動

                break;
            case 3: //停1

                break;
            case 4: //停2

                break;
            case 5: //停3

                break;
            case 6: //停4 

                break;
            case 7: //停5 

                break;
            case 8: //全停 得分
                hardSpaceButtonDown = true;
                break;
        }
    }
    public void SephirothOneButtonLed(int ButtonNumber, byte SwitchLed)
    {
        Status = (CFPGADrvBridge.STATUS)CFPGADrvBridge.FD_SetGPOByIndex(0, (BYTE)ButtonNumber, SwitchLed);
    }

}


public class GetBonusInBonus : Mod_State
{

    public GetBonusInBonus() : base()
    {

        stateName = STATE.GetBonusInBonus;
    }
    float timer = 0;
    public override void Enter()

    {
        //m_SlotMediatorController.SendMessage("m_state", "OpenBlankButton");
        Debug.Log("BonusInBonusEnter");
        timer = 0;
        base.Enter();
    }
    public override void Update()
    {

        m_SlotMediatorController.SendMessage("m_state", "ShowReTriggerPanel");
        timer += Time.deltaTime;
        if (timer > 1.5f)
        {
            Mod_Data.BlankClick = false;
            m_SlotMediatorController.SendMessage("m_state", "CloseTriggerPanel");
            nextState = new BonusSpin();
            nextState.setMediator(m_SlotMediatorController);
            stage = EVENT.EXIT;
        }


    }
    public override void Exit()
    {

        base.Exit();
    }
    public override void SpecialOrder()
    {
        // Debug.Log("TestBonusEnd");
    }
}